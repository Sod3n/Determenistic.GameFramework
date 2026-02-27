using System;

namespace Deterministic.GameFramework.NetworkV2.Buffers;

/// <summary>
/// A simple, reusable, auto-expanding byte buffer.
/// Optimizes for zero-allocation by reusing the underlying array.
/// </summary>
public class PacketBuffer
{
    private byte[] _buffer = new byte[4096];
    private int _head = 0;

    public int Length => _head;

    public Span<byte> GetSpan(int size)
    {
        EnsureCapacity(size);
        return new Span<byte>(_buffer, _head, size);
    }

    public void Advance(int count)
    {
        _head += count;
    }

    public byte[] ToArray()
    {
        // Return a copy of the valid data range.
        // SignalR requires a sized array for transmission.
        // While this is an allocation, it is O(1) per Batch, not O(N) per Action.
        return new ReadOnlySpan<byte>(_buffer, 0, _head).ToArray();
    }

    public void Reset()
    {
        _head = 0;
    }

    private void EnsureCapacity(int additional)
    {
        if (_head + additional > _buffer.Length)
        {
            int newSize = Math.Max(_buffer.Length * 2, _head + additional);
            Array.Resize(ref _buffer, newSize);
        }
    }
}
