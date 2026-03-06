using System;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Physics.Components;

public struct CircleShape2D : IEquatable<CircleShape2D>
{
    public Float Radius;

    public bool Equals(CircleShape2D other)
    {
        return Radius.Equals(other.Radius);
    }

    public override bool Equals(object? obj)
    {
        return obj is CircleShape2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Radius.GetHashCode();
    }

    public static bool operator ==(CircleShape2D left, CircleShape2D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CircleShape2D left, CircleShape2D right)
    {
        return !left.Equals(right);
    }
}