using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct CapsuleShape2D : IEquatable<CapsuleShape2D>
{
    public Float Radius;
    public Float Height;

    public bool Equals(CapsuleShape2D other)
    {
        return Radius.Equals(other.Radius) && Height.Equals(other.Height);
    }

    public override bool Equals(object? obj)
    {
        return obj is CapsuleShape2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Radius, Height);
    }

    public static bool operator ==(CapsuleShape2D left, CapsuleShape2D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CapsuleShape2D left, CapsuleShape2D right)
    {
        return !left.Equals(right);
    }
}