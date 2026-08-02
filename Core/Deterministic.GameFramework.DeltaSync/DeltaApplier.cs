using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Applies a delta op stream (produced by <see cref="DeltaGenerator"/>) to an
/// <see cref="EntityWorld"/>. Straightforward "replacement" semantics — for
/// <see cref="DeltaOpKind.ComponentModify"/>, the new bytes overwrite the current
/// slot directly. Byte-arithmetic reconciliation (predicted-vs-server correction)
/// is handled by <see cref="DeltaSyncStrategyClient"/>, not here.
///
/// Phase 3 note: this is a minimal implementation created to unblock client work.
/// Phase 2 may provide a richer version; if so, this file can be deleted in favor
/// of Phase 2's canonical implementation as long as <see cref="ApplyOps"/>,
/// <see cref="ApplyEntityCreate"/>, and <see cref="ApplyComponentBytes"/> retain
/// their signatures.
/// </summary>
public static class DeltaApplier
{
    /// <summary>
    /// Parses a full <see cref="PacketType.TickDelta"/> packet (header + op stream) and
    /// applies every op to <paramref name="world"/>. Returns the parsed header so callers
    /// can read <see cref="TickDeltaHeader.ServerTick"/> / <see cref="TickDeltaHeader.BaselineTick"/>.
    /// </summary>
    public static TickDeltaHeader ApplyPacket(EntityWorld world, ReadOnlySpan<byte> packet)
    {
        int headerSize = Marshal.SizeOf<TickDeltaHeader>();
        var header = MemoryMarshal.Read<TickDeltaHeader>(packet);
        var payload = packet.Slice(headerSize, header.PayloadLength);
        ApplyOps(world, payload);
        return header;
    }

    /// <summary>
    /// Applies every op in <paramref name="opStream"/> to <paramref name="world"/>.
    /// For Modify ops, writes new bytes directly (no byte arithmetic).
    /// </summary>
    public static void ApplyOps(EntityWorld world, ReadOnlySpan<byte> opStream)
    {
        var reader = new DeltaOpReader(opStream);
        while (reader.TryReadOp(out var op))
        {
            ApplyOp(world, ref reader, in op);
        }
    }

    /// <summary>
    /// Dispatches a single op to the matching apply helper.
    /// </summary>
    public static void ApplyOp(EntityWorld world, ref DeltaOpReader reader, in DeltaOp op)
    {
        switch (op.Kind)
        {
            case DeltaOpKind.EntityCreate:
                ApplyEntityCreate(world, ref reader, in op);
                break;
            case DeltaOpKind.EntityDestroy:
                ApplyEntityDestroy(world, op.EntityId);
                break;
            case DeltaOpKind.ComponentAdd:
                ApplyComponentAdd(world, op.EntityId, op.DenseId, reader.GetDataSlice(in op));
                break;
            case DeltaOpKind.ComponentRemove:
                ApplyComponentRemove(world, op.EntityId, op.DenseId);
                break;
            case DeltaOpKind.ComponentModify:
                ApplyComponentBytes(world, op.EntityId, op.DenseId, reader.GetDataSlice(in op));
                break;
        }
    }

    public static void ApplyEntityCreate(EntityWorld world, ref DeltaOpReader reader, in DeltaOp op)
    {
        int entityId = op.EntityId;
        ulong maskP0 = op.MaskPart0;
        ulong maskP1 = op.MaskPart1;

        EnsureEntityCapacity(world, entityId);

        // The EntityCreate carries the final mask + nested per-component bytes in
        // ascending bit order. We iterate via the reader's walker to avoid copying.
        // Pass entityId by value so the lambda doesn't capture the `in DeltaOp`.
        reader.WalkEntityCreateNested(in op, (world, entityId), (state, bit, data) =>
        {
            ApplyComponentBytes(state.world, state.entityId, bit, data);
        });

        // Ensure mask matches (in case walker missed any bits due to missing stores).
        var masks = world.EntityMasks;
        if (entityId < masks.Length)
        {
            masks[entityId]._part0 = maskP0;
            masks[entityId]._part1 = maskP1;
        }
    }

    public static void ApplyEntityDestroy(EntityWorld world, int entityId)
    {
        if (entityId < 0 || entityId >= world.EntityMasks.Length) return;
        var entity = new Entity(entityId);

        // Clear each component's packed bytes according to the old mask, then clear mask.
        var mask = world.EntityMasks[entityId];
        for (int bit = 0; bit < 128; bit++)
        {
            if (!mask.IsSet(bit)) continue;
            var store = world.GetComponentStore(bit);
            store?.Clear(entityId, 1);
        }
        world.EntityMasks[entityId].Clear();
    }

    public static void ApplyComponentAdd(EntityWorld world, int entityId, int denseId, ReadOnlySpan<byte> data)
    {
        ApplyComponentBytes(world, entityId, denseId, data);
    }

    public static void ApplyComponentRemove(EntityWorld world, int entityId, int denseId)
    {
        if (entityId < 0 || entityId >= world.EntityMasks.Length) return;
        world.EntityMasks[entityId].Unset(denseId);
        var store = world.GetComponentStore(denseId);
        if (store != null && entityId < store.Length)
            store.Clear(entityId, 1);
    }

    /// <summary>
    /// Writes <paramref name="data"/> into the entity's component slot, ensuring the
    /// store exists and the mask bit is set. Used for both ComponentAdd and ComponentModify
    /// (both are replacement-style per the wire contract).
    /// </summary>
    public static void ApplyComponentBytes(EntityWorld world, int entityId, int denseId, ReadOnlySpan<byte> data)
    {
        if (entityId < 0) return;

        EnsureEntityCapacity(world, entityId);
        EnsureStore(world, denseId);

        var store = world.GetComponentStore(denseId);
        if (store == null) return; // Unknown component type on this end — drop silently.

        if (entityId >= store.Length)
        {
            int newLen = Math.Max(store.Length * 2, entityId + 1);
            store.Resize(newLen);
        }

        if (!data.IsEmpty)
            store.WriteElementPacked(entityId, data);

        world.EntityMasks[entityId].Set(denseId);
    }

    /// <summary>Ensures <paramref name="world"/>'s mask array has a slot for <paramref name="entityId"/>.</summary>
    private static void EnsureEntityCapacity(EntityWorld world, int entityId)
    {
        if (entityId < 0) return;

        if (entityId >= world.EntityMasks.Length)
        {
            // ReserveEntityCapacity grows both mask array and existing component stores
            // in lockstep — the public API we want here.
            world.ReserveEntityCapacity(entityId + 1);
        }

        EnsureNextEntityId(world, entityId + 1);
    }

    private static void EnsureStore(EntityWorld world, int denseId)
    {
        if (world.GetComponentStore(denseId) != null) return;

        Type? type = world.GetComponentType(denseId);
        if (type == null && ComponentId.TryGetType(new DenseComponentId(denseId), out var resolvedType))
            type = resolvedType;

        if (type == null) return;

        // Use the internal helper via reflection — EnsureStoreFromType is `internal`.
        _ensureStoreFromType ??= typeof(EntityWorld).GetMethod(
            "EnsureStoreFromType",
            BindingFlags.Instance | BindingFlags.NonPublic);
        _ensureStoreFromType?.Invoke(world, new object[] { denseId, type, 32 });
    }

    private static MethodInfo? _ensureStoreFromType;

    /// <summary>
    /// The <c>_nextEntityId</c> field is internal to the ECS assembly. We bump it via
    /// reflection so subsequent <c>GetComponentStore</c>/iteration sees the entity.
    /// Set once at class init.
    /// </summary>
    private static FieldInfo? _nextEntityIdField;

    private static void EnsureNextEntityId(EntityWorld world, int requiredNext)
    {
        _nextEntityIdField ??= typeof(EntityWorld).GetField(
            "_nextEntityId",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (_nextEntityIdField == null) return;

        int current = (int)_nextEntityIdField.GetValue(world)!;
        if (current < requiredNext)
            _nextEntityIdField.SetValue(world, requiredNext);
    }
}
