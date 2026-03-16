using System;
using System.Text.Json.Serialization;
using FixedMathSharp;

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
    
    // Implicit casts to/from FixedMathSharp.Vector3d
    public static implicit operator Vector3d(Vector3 v) => new Vector3d(v.X.Value, v.Y.Value, v.Z.Value);
    public static implicit operator Vector3(Vector3d v) => new Vector3(new Float(v.x), new Float(v.y), new Float(v.z));

    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);
    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";
    
    [JsonIgnore]
    public Float Magnitude => Float.Sqrt(X * X + Y * Y + Z * Z);
    [JsonIgnore]
    public Float SqrMagnitude => X * X + Y * Y + Z * Z;
    
    [JsonIgnore]
    public Vector3 Normalized
    {
        get
        {
            Float mag = Magnitude;
            return mag > (Float)0 ? this / mag : Zero;
        }
    }

    public static Float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X
    );
    
    public static Float Distance(Vector3 a, Vector3 b) => (a - b).Magnitude;
    public static Float DistanceSquared(Vector3 a, Vector3 b) => (a - b).SqrMagnitude;

    public static Vector3 Lerp(Vector3 a, Vector3 b, Float t)
    {
        t = Float.Clamp(t, 0, 1);
        return new Vector3(
            Float.Lerp(a.X, b.X, t),
            Float.Lerp(a.Y, b.Y, t),
            Float.Lerp(a.Z, b.Z, t)
        );
    }

    public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Float.Min(a.X, b.X), Float.Min(a.Y, b.Y), Float.Min(a.Z, b.Z));
    public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Float.Max(a.X, b.X), Float.Max(a.Y, b.Y), Float.Max(a.Z, b.Z));
}
