using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Produces a <see cref="PacketType.TickDelta"/> payload — a <see cref="TickDeltaHeader"/>
/// followed by a stream of ops (see <see cref="DeltaOpKind"/>) — by diffing a
/// <c>current</c> <see cref="EntityWorld"/> against a <c>baseline</c> <see cref="EntityWorld"/>.
///
/// Algorithm: walk entities from 0 to <c>max(NextEntityId)-1</c>. Per entity:
/// <list type="bullet">
/// <item>Both masks empty → skip.</item>
/// <item>Baseline empty, current has components → <see cref="DeltaOpKind.EntityCreate"/> with
///   inline per-component bytes for each set bit (ascending order).</item>
/// <item>Current empty, baseline had components → <see cref="DeltaOpKind.EntityDestroy"/>.</item>
/// <item>Both populated → per-bit diff. Emits
///   <see cref="DeltaOpKind.ComponentAdd"/>/<see cref="DeltaOpKind.ComponentRemove"/>/<see cref="DeltaOpKind.ComponentModify"/>.</item>
/// </list>
///
/// The writer buffer is reused between calls. Per-element component bytes are copied via
/// <c>stackalloc</c> for small structs (&lt;= 256 bytes) and <see cref="ArrayPool{T}"/> for
/// larger ones, keeping steady-state allocation at zero.
/// </summary>
public sealed class DeltaGenerator
{
    private const int StackBufferThreshold = 256;

    private readonly DeltaOpWriter _writer = new(8192);

    /// <summary>
    /// Generates a complete <see cref="PacketType.TickDelta"/> packet payload
    /// (<see cref="TickDeltaHeader"/> + op stream). Returns a fresh byte[] suitable for
    /// <c>INetworkServer.BroadcastToGroupAsync</c>. The internal op-stream buffer is reused
    /// across calls.
    /// </summary>
    /// <remarks>
    /// Kept for tests and non-hot-path callers that want a right-sized array. The server
    /// broadcast hot path should prefer <see cref="GenerateToPooledBuffer"/> to avoid a
    /// per-tick allocation.
    /// </remarks>
    /// <param name="current">Post-sim state of the current tick.</param>
    /// <param name="baseline">State as of <paramref name="baselineTick"/> (last broadcast).</param>
    /// <param name="serverTick">The tick this delta represents.</param>
    /// <param name="baselineTick">The baseline tick the client must have applied.</param>
    public byte[] Generate(EntityWorld current, EntityWorld baseline, long serverTick, long baselineTick)
    {
        _writer.Reset();
        BuildOpStream(current, baseline);

        int headerSize = Marshal.SizeOf<TickDeltaHeader>();
        int payloadLen = _writer.Length;
        byte[] packet = new byte[headerSize + payloadLen];

        var header = new TickDeltaHeader
        {
            ServerTick = serverTick,
            BaselineTick = baselineTick,
            PayloadLength = payloadLen
        };
        MemoryMarshal.Write(new Span<byte>(packet), ref header);

        _writer.CopyTo(new Span<byte>(packet, headerSize, payloadLen));
        return packet;
    }

    /// <summary>
    /// Generates the same packet as <see cref="Generate"/>, but into a buffer rented from
    /// <see cref="ArrayPool{T}.Shared"/>. The caller owns the rented array and must return
    /// it via <see cref="ArrayPool{T}.Return"/> once the contents are no longer needed —
    /// typically after the buffer has been consumed by
    /// <see cref="Server.IPooledBroadcastNetworkServer.BroadcastToGroupPooledAsync"/>,
    /// which returns it for you.
    ///
    /// The rented array is typically oversized; only <c>[0, length)</c> holds the packet.
    /// </summary>
    /// <returns>(<c>rented</c>, <c>length</c>) — the rented buffer and the valid byte count.</returns>
    public (byte[] rented, int length) GenerateToPooledBuffer(EntityWorld current, EntityWorld baseline, long serverTick, long baselineTick)
    {
        _writer.Reset();
        BuildOpStream(current, baseline);

        int headerSize = Marshal.SizeOf<TickDeltaHeader>();
        int payloadLen = _writer.Length;
        int total = headerSize + payloadLen;

        byte[] rented = ArrayPool<byte>.Shared.Rent(total);

        var header = new TickDeltaHeader
        {
            ServerTick = serverTick,
            BaselineTick = baselineTick,
            PayloadLength = payloadLen
        };
        MemoryMarshal.Write(new Span<byte>(rented, 0, headerSize), ref header);

        _writer.CopyTo(new Span<byte>(rented, headerSize, payloadLen));
        return (rented, total);
    }

    /// <summary>
    /// Generates only the op stream (no header). Useful for tests that want to inspect
    /// or replay ops without constructing a wire packet. The returned span is backed by
    /// the writer's internal buffer and is invalidated by the next call.
    /// </summary>
    public ReadOnlySpan<byte> GenerateOpStream(EntityWorld current, EntityWorld baseline)
    {
        _writer.Reset();
        BuildOpStream(current, baseline);
        return _writer.WrittenSpan;
    }

    private void BuildOpStream(EntityWorld current, EntityWorld baseline)
    {
        int maxEntities = Math.Max(current.NextEntityId, baseline.NextEntityId);
        var currentMasks = current.EntityMasks;
        var baselineMasks = baseline.EntityMasks;

        for (int entityId = 0; entityId < maxEntities; entityId++)
        {
            var currentMask = entityId < current.NextEntityId && entityId < currentMasks.Length
                ? currentMasks[entityId]
                : default;
            var baselineMask = entityId < baseline.NextEntityId && entityId < baselineMasks.Length
                ? baselineMasks[entityId]
                : default;

            bool currentEmpty = currentMask.IsEmpty;
            bool baselineEmpty = baselineMask.IsEmpty;

            if (currentEmpty && baselineEmpty) continue;

            if (baselineEmpty && !currentEmpty)
            {
                EmitEntityCreate(current, entityId, currentMask);
                continue;
            }

            if (currentEmpty && !baselineEmpty)
            {
                _writer.WriteEntityDestroy(entityId);
                continue;
            }

            EmitComponentDiffs(current, baseline, entityId, currentMask, baselineMask);
        }
    }

    private void EmitEntityCreate(EntityWorld current, int entityId, BitMask128 mask)
    {
        _writer.WriteEntityCreate(entityId, new BitMask128Snapshot(mask._part0, mask._part1));

        // Bit-scan the mask's set bits in ascending order. This matches the wire contract
        // (EntityCreate nested records iterate set bits low→high) while skipping every
        // unset bit rather than visiting all 128 positions.
        ulong part0 = mask._part0;
        ulong part1 = mask._part1;

        while (part0 != 0)
        {
            int bit = Ctz64(part0);
            part0 &= part0 - 1; // clear lowest set bit
            WriteCreateBit(current, entityId, bit);
        }
        while (part1 != 0)
        {
            int low = Ctz64(part1);
            part1 &= part1 - 1;
            WriteCreateBit(current, entityId, 64 + low);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteCreateBit(EntityWorld current, int entityId, int bit)
    {
        var store = current.GetComponentStore(bit);
        int elemSize = current.GetComponentElementSize(bit);
        if (store == null || elemSize == 0)
        {
            // Mask bit set but no store registered — emit empty component bytes so the
            // reader can progress. This should only happen if the receiving side lacks
            // the component type, which is handled downstream by the remap logic.
            _writer.WriteNestedComponent(ReadOnlySpan<byte>.Empty);
            return;
        }

        WriteElementBytes(store, entityId, elemSize, copy =>
            _writer.WriteNestedComponent(copy));
    }

    private void EmitComponentDiffs(
        EntityWorld current,
        EntityWorld baseline,
        int entityId,
        BitMask128 currentMask,
        BitMask128 baselineMask)
    {
        // Fast path: when masks match (no structural change), all set bits fall into the
        // "both have it" content-comparison branch. Skip the add/remove checks entirely
        // and iterate only currentMask's set bits. This is the common case for an
        // established entity whose components only change value tick-to-tick.
        if (currentMask._part0 == baselineMask._part0 && currentMask._part1 == baselineMask._part1)
        {
            EmitContentDiffsOnly(current, baseline, entityId, currentMask);
            return;
        }

        // General path: iterate only bits set in (current | baseline). Anything outside
        // that union is !inCurrent && !inBaseline — the no-op case the old linear scan
        // used to spend 95 %+ of its iterations on.
        ulong union0 = currentMask._part0 | baselineMask._part0;
        ulong union1 = currentMask._part1 | baselineMask._part1;

        while (union0 != 0)
        {
            int bit = Ctz64(union0);
            union0 &= union0 - 1;
            EmitDiffBit(current, baseline, entityId, bit, currentMask, baselineMask);
        }
        while (union1 != 0)
        {
            int low = Ctz64(union1);
            union1 &= union1 - 1;
            EmitDiffBit(current, baseline, entityId, 64 + low, currentMask, baselineMask);
        }
    }

    /// <summary>
    /// Specialized diff path for entities whose mask is unchanged since the baseline.
    /// Every set bit is guaranteed to be in both worlds, so we skip the add/remove
    /// branches and jump directly to content comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitContentDiffsOnly(EntityWorld current, EntityWorld baseline, int entityId, BitMask128 mask)
    {
        ulong part0 = mask._part0;
        ulong part1 = mask._part1;

        while (part0 != 0)
        {
            int bit = Ctz64(part0);
            part0 &= part0 - 1;
            EmitModifyIfChanged(current, baseline, entityId, bit);
        }
        while (part1 != 0)
        {
            int low = Ctz64(part1);
            part1 &= part1 - 1;
            EmitModifyIfChanged(current, baseline, entityId, 64 + low);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitDiffBit(
        EntityWorld current,
        EntityWorld baseline,
        int entityId,
        int bit,
        BitMask128 currentMask,
        BitMask128 baselineMask)
    {
        bool inCurrent = currentMask.IsSet(bit);
        bool inBaseline = baselineMask.IsSet(bit);

        // (inCurrent || inBaseline) is guaranteed by the caller (iterating union bits),
        // so the (!inCurrent && !inBaseline) case is unreachable here.

        if (inCurrent && !inBaseline)
        {
            // Added
            var store = current.GetComponentStore(bit);
            int elemSize = current.GetComponentElementSize(bit);
            if (store == null || elemSize == 0)
            {
                _writer.WriteComponentAdd(entityId, bit, ReadOnlySpan<byte>.Empty);
            }
            else
            {
                WriteElementBytes(store, entityId, elemSize, copy =>
                    _writer.WriteComponentAdd(entityId, bit, copy));
            }
            return;
        }

        if (!inCurrent && inBaseline)
        {
            _writer.WriteComponentRemove(entityId, bit);
            return;
        }

        // Both have it — compare packed bytes, emit Modify if different.
        EmitModifyIfChanged(current, baseline, entityId, bit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitModifyIfChanged(EntityWorld current, EntityWorld baseline, int entityId, int bit)
    {
        var curStore = current.GetComponentStore(bit);
        int curElemSize = current.GetComponentElementSize(bit);
        if (curStore == null || curElemSize == 0) return;

        var baseStore = baseline.GetComponentStore(bit);
        int baseElemSize = baseline.GetComponentElementSize(bit);

        // If sizes disagree (shouldn't happen for the same StableId) or baseline lacks the
        // row, treat as unconditional modify (matches legacy behavior).
        if (baseStore == null || baseElemSize == 0 || baseElemSize != curElemSize ||
            entityId >= baseStore.Length)
        {
            WriteElementBytes(curStore, entityId, curElemSize, copy =>
                _writer.WriteComponentModify(entityId, bit, copy));
            return;
        }

        // Compare current vs baseline. Short-circuit if unchanged.
        if (!AreElementBytesEqual(curStore, baseStore, entityId, curElemSize))
        {
            WriteElementBytes(curStore, entityId, curElemSize, copy =>
                _writer.WriteComponentModify(entityId, bit, copy));
        }
    }

    /// <summary>
    /// Copy one element's packed bytes to a span and invoke <paramref name="callback"/>
    /// with it. Stack-allocates for small elements; falls back to pooled array otherwise.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteElementBytes(
        AlignedComponentStore store,
        int index,
        int elemSize,
        SpanCallback callback)
    {
        if (elemSize <= StackBufferThreshold)
        {
            Span<byte> buf = stackalloc byte[StackBufferThreshold];
            var actual = buf.Slice(0, elemSize);
            if (index < store.Length)
                store.CopyElementPacked(index, actual);
            else
                actual.Clear();
            callback(actual);
        }
        else
        {
            byte[] rented = ArrayPool<byte>.Shared.Rent(elemSize);
            try
            {
                var actual = new Span<byte>(rented, 0, elemSize);
                if (index < store.Length)
                    store.CopyElementPacked(index, actual);
                else
                    actual.Clear();
                callback(actual);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private delegate void SpanCallback(ReadOnlySpan<byte> data);

    private static bool AreElementBytesEqual(
        AlignedComponentStore curStore,
        AlignedComponentStore baseStore,
        int index,
        int elemSize)
    {
        if (elemSize <= StackBufferThreshold)
        {
            Span<byte> curBuf = stackalloc byte[StackBufferThreshold];
            Span<byte> baseBuf = stackalloc byte[StackBufferThreshold];
            var a = curBuf.Slice(0, elemSize);
            var b = baseBuf.Slice(0, elemSize);
            curStore.CopyElementPacked(index, a);
            baseStore.CopyElementPacked(index, b);
            return a.SequenceEqual(b);
        }

        byte[] ra = ArrayPool<byte>.Shared.Rent(elemSize);
        byte[] rb = ArrayPool<byte>.Shared.Rent(elemSize);
        try
        {
            var a = new Span<byte>(ra, 0, elemSize);
            var b = new Span<byte>(rb, 0, elemSize);
            curStore.CopyElementPacked(index, a);
            baseStore.CopyElementPacked(index, b);
            return a.SequenceEqual(b);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(ra);
            ArrayPool<byte>.Shared.Return(rb);
        }
    }

    // De Bruijn sequence lookup table for 64-bit trailing-zero count. Used instead of
    // System.Numerics.BitOperations because this assembly multi-targets netstandard2.1,
    // where BitOperations is internal. Caller must guarantee value != 0.
    private static readonly byte[] s_deBruijnCtz64 = new byte[64]
    {
         0,  1, 48,  2, 57, 49, 28,  3,
        61, 58, 50, 42, 38, 29, 17,  4,
        62, 55, 59, 36, 53, 51, 43, 22,
        45, 39, 33, 30, 24, 18, 12,  5,
        63, 47, 56, 27, 60, 41, 37, 16,
        54, 35, 52, 21, 44, 32, 23, 11,
        46, 26, 40, 15, 34, 20, 31, 10,
        25, 14, 19,  9, 13,  8,  7,  6
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Ctz64(ulong value)
    {
        // Isolate lowest set bit, multiply by de Bruijn constant, index table.
        return s_deBruijnCtz64[((value & (ulong)(-(long)value)) * 0x03f79d71b4cb0a89UL) >> 58];
    }
}
