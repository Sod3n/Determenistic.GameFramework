using System;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Cursor-style reader for the delta op stream produced by <see cref="DeltaOpWriter"/>.
/// Shared between server (for debug/loopback round-trip) and client (for applying server
/// deltas in Phase 3). Reads little-endian integers matching the writer's encoding.
///
/// Usage:
/// <code>
/// var reader = new DeltaOpReader(payload);
/// while (reader.TryReadOp(out var op))
/// {
///     switch (op.Kind) { ... }
/// }
/// </code>
/// </summary>
public ref struct DeltaOpReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public DeltaOpReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Position => _position;
    public int Length => _buffer.Length;
    public bool HasMore => _position < _buffer.Length;

    /// <summary>
    /// Reads the next op from the stream. Returns <c>false</c> at end of stream or on
    /// malformed data (truncated payload, unknown op kind). The <paramref name="op"/> out
    /// parameter contains the parsed op including direct spans into the underlying buffer
    /// for component data — do not retain these spans past the buffer's lifetime.
    /// </summary>
    public bool TryReadOp(out DeltaOp op)
    {
        op = default;
        if (_position >= _buffer.Length) return false;

        var kind = (DeltaOpKind)_buffer[_position];
        _position++;

        switch (kind)
        {
            case DeltaOpKind.EntityCreate:
                return TryReadEntityCreate(out op);
            case DeltaOpKind.EntityDestroy:
                return TryReadEntityDestroy(out op);
            case DeltaOpKind.ComponentAdd:
                return TryReadComponentAdd(out op);
            case DeltaOpKind.ComponentRemove:
                return TryReadComponentRemove(out op);
            case DeltaOpKind.ComponentModify:
                return TryReadComponentModify(out op);
            default:
                return false;
        }
    }

    private bool TryReadEntityCreate(out DeltaOp op)
    {
        op = default;
        if (!TryReadInt32(out int entityId)) return false;
        if (!TryReadUInt64(out ulong part0)) return false;
        if (!TryReadUInt64(out ulong part1)) return false;

        op = new DeltaOp
        {
            Kind = DeltaOpKind.EntityCreate,
            EntityId = entityId,
            MaskPart0 = part0,
            MaskPart1 = part1,
            NestedComponentsStart = _position,
            NestedComponentsLength = RemainingUntilEndOfEntityCreate(part0, part1)
        };

        if (op.NestedComponentsLength < 0) return false;
        _position += op.NestedComponentsLength;
        return true;
    }

    private bool TryReadEntityDestroy(out DeltaOp op)
    {
        op = default;
        if (!TryReadInt32(out int entityId)) return false;
        op = new DeltaOp { Kind = DeltaOpKind.EntityDestroy, EntityId = entityId };
        return true;
    }

    private bool TryReadComponentAdd(out DeltaOp op)
    {
        op = default;
        if (!TryReadInt32(out int entityId)) return false;
        if (!TryReadInt32(out int denseId)) return false;
        if (!TryReadInt32(out int dataLen)) return false;
        if (dataLen < 0 || _position + dataLen > _buffer.Length) return false;

        op = new DeltaOp
        {
            Kind = DeltaOpKind.ComponentAdd,
            EntityId = entityId,
            DenseId = denseId,
            DataStart = _position,
            DataLength = dataLen
        };
        _position += dataLen;
        return true;
    }

    private bool TryReadComponentRemove(out DeltaOp op)
    {
        op = default;
        if (!TryReadInt32(out int entityId)) return false;
        if (!TryReadInt32(out int denseId)) return false;

        op = new DeltaOp
        {
            Kind = DeltaOpKind.ComponentRemove,
            EntityId = entityId,
            DenseId = denseId
        };
        return true;
    }

    private bool TryReadComponentModify(out DeltaOp op)
    {
        op = default;
        if (!TryReadInt32(out int entityId)) return false;
        if (!TryReadInt32(out int denseId)) return false;
        if (!TryReadInt32(out int dataLen)) return false;
        if (dataLen < 0 || _position + dataLen > _buffer.Length) return false;

        op = new DeltaOp
        {
            Kind = DeltaOpKind.ComponentModify,
            EntityId = entityId,
            DenseId = denseId,
            DataStart = _position,
            DataLength = dataLen
        };
        _position += dataLen;
        return true;
    }

    /// <summary>
    /// Given a raw buffer slice representing an EntityCreate's nested-components region,
    /// iterate the set bits of the mask in ascending order and read one
    /// <c>(int dataLen, byte[dataLen])</c> record per bit. Yields (denseId, data) pairs by
    /// invoking <paramref name="onComponent"/>. Returns <c>false</c> if the payload is
    /// truncated or malformed.
    /// </summary>
    public static bool TryParseEntityCreateNested(
        ReadOnlySpan<byte> nested,
        ulong maskPart0,
        ulong maskPart1,
        ComponentVisitor onComponent)
    {
        int cursor = 0;
        for (int bit = 0; bit < 128; bit++)
        {
            bool set = bit < 64
                ? (maskPart0 & (1UL << bit)) != 0
                : (maskPart1 & (1UL << (bit - 64))) != 0;
            if (!set) continue;

            if (cursor + 4 > nested.Length) return false;
            int dataLen = ReadInt32(nested.Slice(cursor, 4));
            cursor += 4;
            if (dataLen < 0 || cursor + dataLen > nested.Length) return false;

            onComponent(bit, nested.Slice(cursor, dataLen));
            cursor += dataLen;
        }
        return cursor == nested.Length;
    }

    public delegate void ComponentVisitor(int denseId, ReadOnlySpan<byte> data);

    /// <summary>
    /// Re-parses the nested block starting at <see cref="DeltaOp.NestedComponentsStart"/>
    /// without copying, using the op's mask to guide iteration. Must be called while the
    /// underlying span used to build this reader is still alive. Returns the number of
    /// nested component records consumed, or -1 on malformed input.
    /// </summary>
    public int WalkEntityCreateNested(in DeltaOp op, ComponentVisitor onComponent)
    {
        if (op.Kind != DeltaOpKind.EntityCreate) return -1;
        var slice = _buffer.Slice(op.NestedComponentsStart, op.NestedComponentsLength);
        int count = 0;
        int cursor = 0;
        for (int bit = 0; bit < 128; bit++)
        {
            bool set = bit < 64
                ? (op.MaskPart0 & (1UL << bit)) != 0
                : (op.MaskPart1 & (1UL << (bit - 64))) != 0;
            if (!set) continue;

            if (cursor + 4 > slice.Length) return -1;
            int dataLen = ReadInt32(slice.Slice(cursor, 4));
            cursor += 4;
            if (dataLen < 0 || cursor + dataLen > slice.Length) return -1;

            onComponent(bit, slice.Slice(cursor, dataLen));
            cursor += dataLen;
            count++;
        }
        return cursor == slice.Length ? count : -1;
    }

    /// <summary>
    /// Walk through the nested block without allocating closures. Uses a state parameter
    /// threaded through the visitor. Mirrors <see cref="WalkEntityCreateNested"/>.
    /// </summary>
    public int WalkEntityCreateNested<TState>(in DeltaOp op, TState state, ComponentVisitor<TState> onComponent)
    {
        if (op.Kind != DeltaOpKind.EntityCreate) return -1;
        var slice = _buffer.Slice(op.NestedComponentsStart, op.NestedComponentsLength);
        int count = 0;
        int cursor = 0;
        for (int bit = 0; bit < 128; bit++)
        {
            bool set = bit < 64
                ? (op.MaskPart0 & (1UL << bit)) != 0
                : (op.MaskPart1 & (1UL << (bit - 64))) != 0;
            if (!set) continue;

            if (cursor + 4 > slice.Length) return -1;
            int dataLen = ReadInt32(slice.Slice(cursor, 4));
            cursor += 4;
            if (dataLen < 0 || cursor + dataLen > slice.Length) return -1;

            onComponent(state, bit, slice.Slice(cursor, dataLen));
            cursor += dataLen;
            count++;
        }
        return cursor == slice.Length ? count : -1;
    }

    public delegate void ComponentVisitor<TState>(TState state, int denseId, ReadOnlySpan<byte> data);

    /// <summary>Raw access to the buffer slice for an op's nested block (used by appliers).</summary>
    public ReadOnlySpan<byte> GetNestedSlice(in DeltaOp op)
    {
        if (op.Kind != DeltaOpKind.EntityCreate) return ReadOnlySpan<byte>.Empty;
        return _buffer.Slice(op.NestedComponentsStart, op.NestedComponentsLength);
    }

    /// <summary>Raw component data span for Add/Modify ops.</summary>
    public ReadOnlySpan<byte> GetDataSlice(in DeltaOp op)
    {
        if (op.Kind != DeltaOpKind.ComponentAdd && op.Kind != DeltaOpKind.ComponentModify)
            return ReadOnlySpan<byte>.Empty;
        return _buffer.Slice(op.DataStart, op.DataLength);
    }

    // Pre-compute the span length occupied by nested component records for an EntityCreate.
    // Walks the sub-stream once to validate; position is not advanced here (caller uses the result).
    private int RemainingUntilEndOfEntityCreate(ulong part0, ulong part1)
    {
        int startPos = _position;
        int localCursor = startPos;
        for (int bit = 0; bit < 128; bit++)
        {
            bool set = bit < 64
                ? (part0 & (1UL << bit)) != 0
                : (part1 & (1UL << (bit - 64))) != 0;
            if (!set) continue;

            if (localCursor + 4 > _buffer.Length) return -1;
            int dataLen = ReadInt32(_buffer.Slice(localCursor, 4));
            localCursor += 4;
            if (dataLen < 0 || localCursor + dataLen > _buffer.Length) return -1;
            localCursor += dataLen;
        }
        return localCursor - startPos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadInt32(out int value)
    {
        if (_position + 4 > _buffer.Length) { value = 0; return false; }
        value = ReadInt32(_buffer.Slice(_position, 4));
        _position += 4;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadUInt64(out ulong value)
    {
        if (_position + 8 > _buffer.Length) { value = 0; return false; }
        value = ReadUInt64(_buffer.Slice(_position, 8));
        _position += 8;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(ReadOnlySpan<byte> span)
    {
        return span[0] | (span[1] << 8) | (span[2] << 16) | (span[3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadUInt64(ReadOnlySpan<byte> span)
    {
        return (ulong)span[0]
             | ((ulong)span[1] << 8)
             | ((ulong)span[2] << 16)
             | ((ulong)span[3] << 24)
             | ((ulong)span[4] << 32)
             | ((ulong)span[5] << 40)
             | ((ulong)span[6] << 48)
             | ((ulong)span[7] << 56);
    }
}

/// <summary>
/// Fully decoded op descriptor. For Add/Modify ops, <see cref="DataStart"/>+<see cref="DataLength"/>
/// describe the component bytes inside the original buffer; use
/// <see cref="DeltaOpReader.GetDataSlice"/> to access them safely.
/// For EntityCreate, <see cref="NestedComponentsStart"/>+<see cref="NestedComponentsLength"/>
/// describe the nested <c>(dataLen, bytes)</c> block — use
/// <see cref="DeltaOpReader.WalkEntityCreateNested"/> to iterate it.
/// </summary>
public struct DeltaOp
{
    public DeltaOpKind Kind;
    public int EntityId;
    public int DenseId;

    // EntityCreate only
    public ulong MaskPart0;
    public ulong MaskPart1;
    public int NestedComponentsStart;
    public int NestedComponentsLength;

    // ComponentAdd / ComponentModify only
    public int DataStart;
    public int DataLength;
}
