using System;

namespace Deterministic.GameFramework.CoreV2;

public readonly record struct StableComponentId(Guid Value) : IComparable<StableComponentId>
{
    public static implicit operator Guid(StableComponentId id) => id.Value;
    public static explicit operator StableComponentId(Guid value) => new(value);
    public override string ToString() => $"Stable({Value})";

    public int CompareTo(StableComponentId other)
    {
        return Value.CompareTo(other.Value);
    }
}
