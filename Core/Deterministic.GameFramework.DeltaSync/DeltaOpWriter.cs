using System;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Cursor-style writer for the delta op stream described in <see cref="DeltaOpKind"/>.
/// Maintains a resizing byte[] buffer reused across ticks for zero steady-state allocation.
/// Layout matches <see cref="DeltaOpReader"/> exactly:
/// <list type="bullet">
/// <item>1 byte <see cref="DeltaOpKind"/></item>
/// <item>Op-specific payload (varies — see <c>DeltaPackets.cs</c> for contract)</item>
/// </list>
/// All integers are little-endian (matches <see cref="System.Buffers.Binary.BinaryPrimitives"/>
/// little-endian variants and the <see cref="System.Runtime.InteropServices.MemoryMarshal"/>
/// writes used elsewhere in this assembly).
/// </summary>
public sealed class DeltaOpWriter
{
    private byte[] _buffer;
    private int _head;

    public DeltaOpWriter(int initialCapacity = 4096)
    {
        _buffer = new byte[Math.Max(16, initialCapacity)];
        _head = 0;
    }

    /// <summary>Number of valid bytes currently staged in the buffer.</summary>
    public int Length => _head;

    /// <summary>Direct view over the staged bytes. Invalidated by the next write.</summary>
    public ReadOnlySpan<byte> WrittenSpan => new(_buffer, 0, _head);

    /// <summary>Rewind the cursor to zero, keeping the buffer allocation.</summary>
    public void Reset() => _head = 0;

    /// <summary>Copy the valid range into a newly allocated array for transport.</summary>
    public byte[] ToArray() => new ReadOnlySpan<byte>(_buffer, 0, _head).ToArray();

    /// <summary>
    /// Copy the valid range into <paramref name="dest"/> starting at <paramref name="offset"/>.
    /// Returns the number of bytes written. Used by <see cref="DeltaGenerator"/> to splice the
    /// op stream into a pre-sized packet with a header prefix.
    /// </summary>
    public int CopyTo(Span<byte> dest)
    {
        new ReadOnlySpan<byte>(_buffer, 0, _head).CopyTo(dest);
        return _head;
    }

    public void WriteEntityCreate(int entityId, BitMask128Snapshot mask)
    {
        EnsureCapacity(1 + 4 + 16);
        _buffer[_head++] = (byte)DeltaOpKind.EntityCreate;
        WriteInt32(entityId);
        WriteUInt64(mask.Part0);
        WriteUInt64(mask.Part1);
    }

    public void WriteEntityDestroy(int entityId)
    {
        EnsureCapacity(1 + 4);
        _buffer[_head++] = (byte)DeltaOpKind.EntityDestroy;
        WriteInt32(entityId);
    }

    public void WriteComponentAdd(int entityId, int denseId, ReadOnlySpan<byte> data)
    {
        EnsureCapacity(1 + 4 + 4 + 4 + data.Length);
        _buffer[_head++] = (byte)DeltaOpKind.ComponentAdd;
        WriteInt32(entityId);
        WriteInt32(denseId);
        WriteInt32(data.Length);
        data.CopyTo(new Span<byte>(_buffer, _head, data.Length));
        _head += data.Length;
    }

    public void WriteComponentRemove(int entityId, int denseId)
    {
        EnsureCapacity(1 + 4 + 4);
        _buffer[_head++] = (byte)DeltaOpKind.ComponentRemove;
        WriteInt32(entityId);
        WriteInt32(denseId);
    }

    public void WriteComponentModify(int entityId, int denseId, ReadOnlySpan<byte> data)
    {
        EnsureCapacity(1 + 4 + 4 + 4 + data.Length);
        _buffer[_head++] = (byte)DeltaOpKind.ComponentModify;
        WriteInt32(entityId);
        WriteInt32(denseId);
        WriteInt32(data.Length);
        data.CopyTo(new Span<byte>(_buffer, _head, data.Length));
        _head += data.Length;
    }

    /// <summary>
    /// Writes the nested per-component payload for an EntityCreate body.
    /// Format: <c>int dataLen</c>, <c>byte[dataLen]</c>. Called once per set bit in the mask,
    /// in ascending bit-index order. Not prefixed by a <see cref="DeltaOpKind"/> byte.
    /// </summary>
    public void WriteNestedComponent(ReadOnlySpan<byte> data)
    {
        EnsureCapacity(4 + data.Length);
        WriteInt32(data.Length);
        data.CopyTo(new Span<byte>(_buffer, _head, data.Length));
        _head += data.Length;
    }

    /// <summary>
    /// Copies raw bytes verbatim into the buffer. Used by <see cref="DeltaSubtractor"/> to
    /// splice an EntityCreate's nested-components block from a source op stream into the
    /// target stream without re-parsing every individual component.
    /// </summary>
    public void WriteRaw(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;
        EnsureCapacity(data.Length);
        data.CopyTo(new Span<byte>(_buffer, _head, data.Length));
        _head += data.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteInt32(int value)
    {
        _buffer[_head] = (byte)value;
        _buffer[_head + 1] = (byte)(value >> 8);
        _buffer[_head + 2] = (byte)(value >> 16);
        _buffer[_head + 3] = (byte)(value >> 24);
        _head += 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteUInt64(ulong value)
    {
        _buffer[_head] = (byte)value;
        _buffer[_head + 1] = (byte)(value >> 8);
        _buffer[_head + 2] = (byte)(value >> 16);
        _buffer[_head + 3] = (byte)(value >> 24);
        _buffer[_head + 4] = (byte)(value >> 32);
        _buffer[_head + 5] = (byte)(value >> 40);
        _buffer[_head + 6] = (byte)(value >> 48);
        _buffer[_head + 7] = (byte)(value >> 56);
        _head += 8;
    }

    private void EnsureCapacity(int additional)
    {
        int required = _head + additional;
        if (required <= _buffer.Length) return;

        int newSize = Math.Max(_buffer.Length * 2, required);
        Array.Resize(ref _buffer, newSize);
    }
}

/// <summary>
/// Small value-type snapshot of a BitMask128's two ulong words, used to decouple the writer
/// from the <c>Deterministic.GameFramework.Types.BitMask128</c> struct layout. The generator
/// fills this from the public <c>_part0</c>/<c>_part1</c> fields.
/// </summary>
public readonly struct BitMask128Snapshot
{
    public readonly ulong Part0;
    public readonly ulong Part1;

    public BitMask128Snapshot(ulong part0, ulong part1)
    {
        Part0 = part0;
        Part1 = part1;
    }
}
