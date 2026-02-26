using System;

namespace Deterministic.GameFramework.CoreV2;

public struct Vector2 : IParam, IEquatable<Vector2>
{
    public Float X;
    public Float Y;

    public Vector2(Float x, Float y)
    {
        X = x;
        Y = y;
    }

    public Vector2(float x, float y)
    {
        X = new Float(x);
        Y = new Float(y);
    }

    public static Vector2 Zero => new Vector2(0f, 0f);
    public static Vector2 One => new Vector2(1f, 1f);
    public static Vector2 Right => new Vector2(1f, 0f);
    public static Vector2 Left => new Vector2(-1f, 0f);
    public static Vector2 Up => new Vector2(0f, 1f);
    public static Vector2 Down => new Vector2(0f, -1f);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 a, Float b) => new Vector2(a.X * b, a.Y * b);
    public static Vector2 operator *(Float b, Vector2 a) => new Vector2(a.X * b, a.Y * b);
    public static Vector2 operator /(Vector2 a, Float b) => new Vector2(a.X / b, a.Y / b);

    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);
    public bool Equals(Vector2 other) => X == other.X && Y == other.Y;
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";
    
    public Float Magnitude => Float.Sqrt(X * X + Y * Y);
    public Float SqrMagnitude => X * X + Y * Y;
    
    public Vector2 Normalized
    {
        get
        {
            Float mag = Magnitude;
            return mag > 0 ? this / mag : Zero;
        }
    }
}
