using  System;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics2D.Components;

public struct RectangleShape2D : IEquatable<RectangleShape2D>
{
    public Vector2 Size;

    public bool Equals(RectangleShape2D other)
    {
        return Size.Equals(other.Size);
    }

    public override bool Equals(object? obj)
    {
        return obj is RectangleShape2D other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Size.GetHashCode();
    }

    public static bool operator ==(RectangleShape2D left, RectangleShape2D right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(RectangleShape2D left, RectangleShape2D right)
    {
        return !left.Equals(right);
    }
}