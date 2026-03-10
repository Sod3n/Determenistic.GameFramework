using System;

namespace Deterministic.GameFramework.CoreV2;

public readonly record struct DenseComponentId(int Value) : IComparable<DenseComponentId>
{
    public static implicit operator int(DenseComponentId id) => id.Value;
    public static explicit operator DenseComponentId(int value) => new(value);
    public override string ToString() => $"Local({Value})";

    public int CompareTo(DenseComponentId other)
    {
        return Value.CompareTo(other.Value);
    }
}
