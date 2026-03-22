using System;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Types;

/// <summary>
/// A 128-bit mask used to track component presence on entities efficiently.
/// Replaces HashSets for O(1) checks.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct BitMask128 : IParam, IEquatable<BitMask128>
{
    public ulong _part0; // Bits 0-63
    public ulong _part1; // Bits 64-127

    public void Set(int bitIndex)
    {
        if (bitIndex < 64)
            _part0 |= (1UL << bitIndex);
        else
            _part1 |= (1UL << (bitIndex - 64));
    }

    public void Unset(int bitIndex)
    {
        if (bitIndex < 64)
            _part0 &= ~(1UL << bitIndex);
        else
            _part1 &= ~(1UL << (bitIndex - 64));
    }

    public bool IsSet(int bitIndex)
    {
        if (bitIndex < 64)
            return (_part0 & (1UL << bitIndex)) != 0;
        else
            return (_part1 & (1UL << (bitIndex - 64))) != 0;
    }

    public void Clear()
    {
        _part0 = 0;
        _part1 = 0;
    }

    public bool IsEmpty => _part0 == 0 && _part1 == 0;

    public void Or(BitMask128 other)
    {
        _part0 |= other._part0;
        _part1 |= other._part1;
    }

    public void And(BitMask128 other)
    {
        _part0 &= other._part0;
        _part1 &= other._part1;
    }

    public bool HasAll(BitMask128 other)
    {
        return (_part0 & other._part0) == other._part0 &&
               (_part1 & other._part1) == other._part1;
    }

    public bool HasAny(BitMask128 other)
    {
        return (_part0 & other._part0) != 0 ||
               (_part1 & other._part1) != 0;
    }

    public override bool Equals(object? obj) => obj is BitMask128 other && Equals(other);
    public bool Equals(BitMask128 other) => _part0 == other._part0 && _part1 == other._part1;
    public override int GetHashCode() => HashCode.Combine(_part0, _part1);

    public static bool operator ==(BitMask128 a, BitMask128 b) => a.Equals(b);
    public static bool operator !=(BitMask128 a, BitMask128 b) => !a.Equals(b);
}
