using System;

namespace Deterministic.GameFramework.CoreV2;

public struct Vector3 : IParam, IEquatable<Vector3>
{
    public Float X;
    public Float Y;
    public Float Z;

    public Vector3(Float x, Float y, Float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(float x, float y, float z)
    {
        X = new Float(x);
        Y = new Float(y);
        Z = new Float(z);
    }

    public static Vector3 Zero => new Vector3(0f, 0f, 0f);
    public static Vector3 One => new Vector3(1f, 1f, 1f);
    public static Vector3 Right => new Vector3(1f, 0f, 0f);
    public static Vector3 Left => new Vector3(-1f, 0f, 0f);
    public static Vector3 Up => new Vector3(0f, 1f, 0f);
    public static Vector3 Down => new Vector3(0f, -1f, 0f);
    public static Vector3 Forward => new Vector3(0f, 0f, 1f);
    public static Vector3 Back => new Vector3(0f, 0f, -1f);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3 operator *(Vector3 a, Float b) => new Vector3(a.X * b, a.Y * b, a.Z * b);
    public static Vector3 operator *(Float b, Vector3 a) => new Vector3(a.X * b, a.Y * b, a.Z * b);
    public static Vector3 operator /(Vector3 a, Float b) => new Vector3(a.X / b, a.Y / b, a.Z / b);

    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
    
    public Float Magnitude => Float.Sqrt(X * X + Y * Y + Z * Z);
    public Float SqrMagnitude => X * X + Y * Y + Z * Z;
    
    public Vector3 Normalized
    {
        get
        {
            Float mag = Magnitude;
            return mag > 0 ? this / mag : Zero;
        }
    }
}
