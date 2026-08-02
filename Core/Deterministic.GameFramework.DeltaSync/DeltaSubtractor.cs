using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Algebraic delta subtraction: <c>TargetDelta = ServerDelta - ClientDelta</c>.
/// <para>
/// When the client has a recorded predicted per-tick delta for tick T and the server's
/// authoritative delta for the same tick arrives, the TargetDelta is the minimal op stream
/// that, applied to the client's current state, brings it to what the server says the state
/// should be at T — preserving predictions that matched and snapping the ones that didn't.
/// </para>
/// <para>Semantics per op:</para>
/// <list type="bullet">
/// <item><b>Server op with matching client op (same bytes)</b> — prediction correct. Emit nothing.</item>
/// <item><b>Server op differs from / unmatched by client</b> — prediction wrong or server-only. Emit the server op (snap-to-server).</item>
/// <item><b>Client op not present in server delta</b> — client predicted something server didn't do. Emit a REVERT op using the baseline's bytes (restore pre-prediction value) or a trivially-inverse op (EntityDestroy for EntityCreate, ComponentRemove for ComponentAdd, etc.).</item>
/// </list>
/// <para>
/// In the common case (deterministic prediction, action accepted at matching tick), every
/// server op matches its client counterpart byte-for-byte and TargetDelta is empty — no
/// snap-back, client's further-advanced state is preserved.
/// </para>
/// </summary>
public static class DeltaSubtractor
{
    // Reusable collections — cleared at start of each Subtract call.
    // Avoids per-delta Dictionary/HashSet allocation.
    [ThreadStatic] private static Dictionary<OpKey, DeltaOp>? _clientIndexCache;
    [ThreadStatic] private static HashSet<OpKey>? _touchedKeysCache;

    private readonly struct OpKey : IEquatable<OpKey>
    {
        public readonly int EntityId;
        public readonly int DenseId;
        public readonly DeltaOpKind Kind;

        public OpKey(int entityId, int denseId, DeltaOpKind kind)
        {
            EntityId = entityId;
            DenseId = denseId;
            Kind = kind;
        }

        public bool Equals(OpKey other) => EntityId == other.EntityId && DenseId == other.DenseId && Kind == other.Kind;
        public override bool Equals(object? obj) => obj is OpKey k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(EntityId, DenseId, (int)Kind);
    }

    private static OpKey KeyOf(in DeltaOp op)
    {
        // Entity-level ops key on (entity, -1, kind). Component ops key on (entity, denseId, kind).
        int denseKey = (op.Kind == DeltaOpKind.EntityCreate || op.Kind == DeltaOpKind.EntityDestroy)
            ? -1
            : op.DenseId;
        return new OpKey(op.EntityId, denseKey, op.Kind);
    }

    /// <summary>
    /// Writes <c>serverOps - clientOps</c> into <paramref name="output"/>. The produced op stream,
    /// when applied to the client's live state via <see cref="DeltaApplier.ApplyOps"/>, reconciles
    /// the client to server's authoritative state for the tick represented by the server delta.
    /// </summary>
    /// <param name="baseline">Server's last-confirmed state (before this tick). Used as the
    /// source of "pre-prediction" bytes for revert ops.</param>
    public static void Subtract(
        ReadOnlySpan<byte> serverOps,
        ReadOnlySpan<byte> clientOps,
        EntityWorld baseline,
        DeltaOpWriter output)
    {
        // Pass 1: index every client op by (entity, denseId, kind).
        var clientIndex = _clientIndexCache ??= new Dictionary<OpKey, DeltaOp>();
        clientIndex.Clear();
        {
            var reader = new DeltaOpReader(clientOps);
            while (reader.TryReadOp(out var op))
                clientIndex[KeyOf(op)] = op;
        }

        // Pass 2: walk server ops. Emit whatever the client didn't predict identically.
        // Track which client-keys were "touched" so the revert pass doesn't emit a revert
        // for something the server already snapped.
        var touchedClientKeys = _touchedKeysCache ??= new HashSet<OpKey>();
        touchedClientKeys.Clear();
        {
            var reader = new DeltaOpReader(serverOps);
            while (reader.TryReadOp(out var serverOp))
            {
                var key = KeyOf(serverOp);
                if (clientIndex.TryGetValue(key, out var clientOp)
                    && OpsEqual(in serverOp, serverOps, in clientOp, clientOps))
                {
                    // Perfect match — client's prediction hit server's authoritative value.
                    touchedClientKeys.Add(key);
                    continue;
                }

                EmitOp(output, in serverOp, serverOps);
                touchedClientKeys.Add(key);
            }
        }

        // Pass 3: walk client ops. For each NOT touched, emit a revert — the server said
        // nothing about this, so it's a mispredicted change and we undo it.
        {
            var reader = new DeltaOpReader(clientOps);
            while (reader.TryReadOp(out var clientOp))
            {
                var key = KeyOf(clientOp);
                if (touchedClientKeys.Contains(key)) continue;

                EmitRevert(output, baseline, in clientOp, clientOps);
            }
        }
    }

    private static bool OpsEqual(
        in DeltaOp a, ReadOnlySpan<byte> aBuf,
        in DeltaOp b, ReadOnlySpan<byte> bBuf)
    {
        // Kind is already equal (same OpKey).
        switch (a.Kind)
        {
            case DeltaOpKind.EntityDestroy:
            case DeltaOpKind.ComponentRemove:
                return true; // no payload to compare
            case DeltaOpKind.ComponentAdd:
            case DeltaOpKind.ComponentModify:
                if (a.DataLength != b.DataLength) return false;
                return aBuf.Slice(a.DataStart, a.DataLength)
                    .SequenceEqual(bBuf.Slice(b.DataStart, b.DataLength));
            case DeltaOpKind.EntityCreate:
                if (a.MaskPart0 != b.MaskPart0) return false;
                if (a.MaskPart1 != b.MaskPart1) return false;
                if (a.NestedComponentsLength != b.NestedComponentsLength) return false;
                return aBuf.Slice(a.NestedComponentsStart, a.NestedComponentsLength)
                    .SequenceEqual(bBuf.Slice(b.NestedComponentsStart, b.NestedComponentsLength));
            default:
                return false;
        }
    }

    private static void EmitOp(DeltaOpWriter output, in DeltaOp op, ReadOnlySpan<byte> buf)
    {
        switch (op.Kind)
        {
            case DeltaOpKind.EntityCreate:
                output.WriteEntityCreate(op.EntityId, new BitMask128Snapshot(op.MaskPart0, op.MaskPart1));
                output.WriteRaw(buf.Slice(op.NestedComponentsStart, op.NestedComponentsLength));
                break;
            case DeltaOpKind.EntityDestroy:
                output.WriteEntityDestroy(op.EntityId);
                break;
            case DeltaOpKind.ComponentAdd:
                output.WriteComponentAdd(op.EntityId, op.DenseId, buf.Slice(op.DataStart, op.DataLength));
                break;
            case DeltaOpKind.ComponentRemove:
                output.WriteComponentRemove(op.EntityId, op.DenseId);
                break;
            case DeltaOpKind.ComponentModify:
                output.WriteComponentModify(op.EntityId, op.DenseId, buf.Slice(op.DataStart, op.DataLength));
                break;
        }
    }

    /// <summary>
    /// Emit the inverse of a client-only op. For entity/component additions we can invert
    /// without the baseline. For Modify/Remove we need the baseline's pre-prediction bytes.
    /// If the baseline doesn't have the component (client predicted a change to something that
    /// didn't exist yet server-side), fall back to ComponentRemove as a best-effort.
    /// </summary>
    private static void EmitRevert(
        DeltaOpWriter output,
        EntityWorld baseline,
        in DeltaOp op,
        ReadOnlySpan<byte> buf)
    {
        switch (op.Kind)
        {
            case DeltaOpKind.EntityCreate:
                // Client created an entity server doesn't know about → destroy it locally.
                output.WriteEntityDestroy(op.EntityId);
                break;

            case DeltaOpKind.ComponentAdd:
                // Client added a component server doesn't have → remove it.
                output.WriteComponentRemove(op.EntityId, op.DenseId);
                break;

            case DeltaOpKind.ComponentModify:
                // Client changed a value server didn't change → restore baseline value.
                if (TryReadBaselineBytes(baseline, op.EntityId, op.DenseId, out var prevBytes))
                    output.WriteComponentModify(op.EntityId, op.DenseId, prevBytes);
                else
                    output.WriteComponentRemove(op.EntityId, op.DenseId);
                break;

            case DeltaOpKind.ComponentRemove:
                // Client removed a component server still has → re-add from baseline.
                if (TryReadBaselineBytes(baseline, op.EntityId, op.DenseId, out var restoreBytes))
                    output.WriteComponentAdd(op.EntityId, op.DenseId, restoreBytes);
                break;

            case DeltaOpKind.EntityDestroy:
                // Reviving a destroyed entity requires re-emitting its full mask + components
                // from baseline. Client-predicted destroys are uncommon (prediction mostly adds
                // state); punt until a concrete use-case appears.
                break;
        }
    }

    private static bool TryReadBaselineBytes(EntityWorld baseline, int entityId, int denseId, out ReadOnlySpan<byte> bytes)
    {
        bytes = default;
        if (entityId < 0 || entityId >= baseline.EntityMasks.Length) return false;
        if (!baseline.EntityMasks[entityId].IsSet(denseId)) return false;
        var store = baseline.GetComponentStore(denseId);
        if (store == null || entityId >= store.Length) return false;

        int size = store.ElementSize;
        if (size <= 0) return false;

        byte[] buf = new byte[size];
        store.CopyElementPacked(entityId, buf);
        bytes = buf;
        return true;
    }
}
