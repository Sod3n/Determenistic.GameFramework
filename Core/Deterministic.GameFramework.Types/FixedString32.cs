using System;
using System.Text;

namespace Deterministic.GameFramework.Types;

/// <summary>
/// A fixed-size string buffer (32 bytes) suitable for deterministic networking.
/// </summary>
public struct FixedString32 : IParam, IEquatable<FixedString32>
{
    // Using long to pack 8 bytes at a time for 32 bytes total
    private long _part0;
    private long _part1;
    private long _part2;
    private long _part3;
    
    public const int MaxLength = 32;

    public FixedString32(string s)
    {
        _part0 = 0;
        _part1 = 0;
        _part2 = 0;
        _part3 = 0;
        Set(s);
    }

    public void Set(string s)
    {
        _part0 = _part1 = _part2 = _part3 = 0;
        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[MaxLength];
        
        // Use Encoder to convert without allocation and handle truncation gracefully
        var encoder = Encoding.UTF8.GetEncoder();
        encoder.Convert(s.AsSpan(), buffer, true, out int charsUsed, out int bytesUsed, out bool completed);
        
        // Pack into longs
        _part0 = BitConverter.ToInt64(buffer.Slice(0, 8));
        _part1 = BitConverter.ToInt64(buffer.Slice(8, 8));
        _part2 = BitConverter.ToInt64(buffer.Slice(16, 8));
        _part3 = BitConverter.ToInt64(buffer.Slice(24, 8));
    }

    public override string ToString()
    {
        Span<byte> buffer = stackalloc byte[MaxLength];
        BitConverter.TryWriteBytes(buffer.Slice(0, 8), _part0);
        BitConverter.TryWriteBytes(buffer.Slice(8, 8), _part1);
        BitConverter.TryWriteBytes(buffer.Slice(16, 8), _part2);
        BitConverter.TryWriteBytes(buffer.Slice(24, 8), _part3);

        // Find null terminator or end
        int len = 0;
        while (len < MaxLength && buffer[len] != 0)
        {
            len++;
        }
        
        return Encoding.UTF8.GetString(buffer.Slice(0, len));
    }

    public static implicit operator string(FixedString32 fs) => fs.ToString();
    public static implicit operator FixedString32(string s) => new FixedString32(s);

    public bool Equals(FixedString32 other)
    {
        return _part0 == other._part0 &&
               _part1 == other._part1 &&
               _part2 == other._part2 &&
               _part3 == other._part3;
    }

    public override bool Equals(object? obj) => obj is FixedString32 other && Equals(other);
    
    public override int GetHashCode()
    {
        return HashCode.Combine(_part0, _part1, _part2, _part3);
    }

    public static bool operator ==(FixedString32 a, FixedString32 b) => a.Equals(b);
    public static bool operator !=(FixedString32 a, FixedString32 b) => !a.Equals(b);
}
