using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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