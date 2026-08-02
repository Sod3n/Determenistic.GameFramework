using System;
using System.Runtime.CompilerServices;

namespace Deterministic.GameFramework.ECS;

/// <summary>
/// Non-generic base for type-erased access to aligned component storage.
/// </summary>
public abstract class AlignedComponentStore
{
    public abstract int Length { get; }
    public abstract int ElementSize { get; }
    public abstract int Stride { get; }
    public abstract void Clear(int index, int count);
    public abstract void Resize(int newLength);
    public abstract object? GetValue(int index);

    /// <summary>
    /// Serializes elements tightly packed (no alignment padding) for network/disk.
    /// </summary>
    public abstract byte[] SerializePacked(int count);

    /// <summary>
    /// Deserializes tightly packed bytes into aligned slots.
    /// </summary>
    public abstract void DeserializePacked(ReadOnlyMemory<byte> data, int elementCount);

    /// <summary>
    /// Copies the tightly packed bytes (<see cref="ElementSize"/> bytes, no alignment padding)
    /// of one element into <paramref name="dest"/>. Used by delta generators that need
    /// per-entity component bytes without serializing the entire store.
    /// </summary>
    public abstract void CopyElementPacked(int index, Span<byte> dest);

    /// <summary>
    /// Writes tightly packed bytes (<see cref="ElementSize"/> bytes) into the element slot.
    /// Inverse of <see cref="CopyElementPacked"/>.
    /// </summary>
    public abstract void WriteElementPacked(int index, ReadOnlySpan<byte> src);
}

/// <summary>
/// Stores components in a byte[] buffer with 8-byte aligned element slots.
/// This prevents EXC_ARM_DA_ALIGN crashes on Apple Silicon when Pack=1 structs
/// contain long/ulong fields (e.g., FixedString32, Fixed64, Vector2).
///
/// WARNING: Resize() allocates a new backing buffer. Any 'ref T' obtained via
/// Get() before a Resize() becomes a dangling reference — reads return stale data,
/// writes are silently lost. Always re-obtain refs after CreateEntity()/AddComponent()
/// that may trigger store growth.
/// </summary>
public sealed class AlignedComponentStore<T> : AlignedComponentStore where T : struct
{
    private byte[] _buffer;
    private int _length;
    private readonly int _elementSize;
    private readonly int _stride;

    public override int Length => _length;
    public override int ElementSize => _elementSize;
    public override int Stride => _stride;

    public AlignedComponentStore(int capacity)
    {
        _elementSize = Unsafe.SizeOf<T>();
        _stride = (_elementSize + 7) & ~7; // Round up to multiple of 8
        _length = capacity;
        _buffer = new byte[capacity * _stride];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T Get(int index)
    {
        return ref Unsafe.As<byte, T>(ref _buffer[index * _stride]);
    }

    public override void Clear(int index, int count)
    {
        int offset = index * _stride;
        int bytes = count * _stride;
        if (offset + bytes > _buffer.Length)
            bytes = _buffer.Length - offset;
        if (bytes > 0)
            Array.Clear(_buffer, offset, bytes);
    }

    public override void Resize(int newLength)
    {
        var newBuffer = new byte[newLength * _stride];
        int copyBytes = Math.Min(_length * _stride, newBuffer.Length);
        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, copyBytes);
#if DEBUG
        // Poison the old buffer so any stale ref (obtained before this Resize)
        // reads obviously wrong data instead of silently succeeding.
        // 0xCD = classic "uninitialized heap" marker. Entity IDs become -842150451,
        // booleans become non-0/1, floats become NaN — all immediately suspicious.
        Array.Fill(_buffer, (byte)0xCD);
#endif
        // New portion is already zeroed by .NET array allocation
        _buffer = newBuffer;
        _length = newLength;
    }

    public override object? GetValue(int index)
    {
        return Get(index);
    }

    public override byte[] SerializePacked(int count)
    {
        if (count == 0) return Array.Empty<byte>();

        byte[] result = new byte[count * _elementSize];

        if (_elementSize == _stride)
        {
            // No padding — direct copy
            Buffer.BlockCopy(_buffer, 0, result, 0, result.Length);
        }
        else
        {
            // Strip padding per element
            for (int i = 0; i < count; i++)
            {
                Buffer.BlockCopy(_buffer, i * _stride, result, i * _elementSize, _elementSize);
            }
        }

        return result;
    }

    public override void DeserializePacked(ReadOnlyMemory<byte> data, int elementCount)
    {
        var span = data.Span;
        int count = Math.Min(elementCount, _length);

        // Clear buffer to ensure padding bytes are zero (determinism)
        Array.Clear(_buffer, 0, _buffer.Length);

        if (_elementSize == _stride)
        {
            // No padding — direct copy
            int copyBytes = Math.Min(span.Length, count * _elementSize);
            span.Slice(0, copyBytes).CopyTo(new Span<byte>(_buffer, 0, copyBytes));
        }
        else
        {
            // Insert into aligned slots
            for (int i = 0; i < count && (i * _elementSize + _elementSize) <= span.Length; i++)
            {
                span.Slice(i * _elementSize, _elementSize)
                    .CopyTo(new Span<byte>(_buffer, i * _stride, _elementSize));
            }
        }
    }

    public override void CopyElementPacked(int index, Span<byte> dest)
    {
        new ReadOnlySpan<byte>(_buffer, index * _stride, _elementSize).CopyTo(dest);
    }

    public ReadOnlySpan<byte> GetElementSpan(int index)
    {
        return new ReadOnlySpan<byte>(_buffer, index * _stride, _elementSize);
    }

    public override void WriteElementPacked(int index, ReadOnlySpan<byte> src)
    {
        // Clear the slot (including padding) to keep determinism bytes zero, then write.
        var slot = new Span<byte>(_buffer, index * _stride, _stride);
        slot.Clear();
        src.Slice(0, Math.Min(src.Length, _elementSize)).CopyTo(slot);
    }

    public ReadOnlySpan<byte> GetBufferSpan() => _buffer;

    public int BufferLength => _buffer.Length;
}
