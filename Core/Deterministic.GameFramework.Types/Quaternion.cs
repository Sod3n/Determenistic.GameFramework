using System;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using FixedMathSharp;

namespace Deterministic.GameFramework.Types;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Quaternion : IParam, IEquatable<Quaternion>
{
    public Float X;
    public Float Y;
    public Float Z;
    public Float W;

    public Quaternion(Float x, Float y, Float z, Float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Quaternion(float x, float y, float z, float w)
    {
        X = new Float(x);
        Y = new Float(y);
        Z = new Float(z);
        W = new Float(w);
    }

    public static Quaternion Identity => new Quaternion(0f, 0f, 0f, 1f);

    public static bool operator ==(Quaternion a, Quaternion b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

    public static Quaternion operator *(Quaternion a, Quaternion b) => (Quaternion)((FixedQuaternion)a * (FixedQuaternion)b);
    public static Vector3 operator *(Quaternion rotation, Vector3 point) => (Vector3)((FixedQuaternion)rotation * (Vector3d)point);

    // Implicit casts to/from FixedMathSharp.FixedQuaternion
    public static implicit operator FixedQuaternion(Quaternion q) => new FixedQuaternion(q.X.Value, q.Y.Value, q.Z.Value, q.W.Value);
    public static implicit operator Quaternion(FixedQuaternion q) => new Quaternion(new Float(q.x), new Float(q.y), new Float(q.z), new Float(q.w));

    public override bool Equals(object? obj) => obj is Quaternion other && Equals(other);
    public bool Equals(Quaternion other) => X == other.X && Y == other.Y && Z == other.Z && W == other.W;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => $"({X}, {Y}, {Z}, {W})";

    public static Quaternion FromEuler(Vector3 euler)
    {
        Float cy = Float.Cos(euler.Z * new Float(0.5f));
        Float sy = Float.Sin(euler.Z * new Float(0.5f));
        Float cp = Float.Cos(euler.Y * new Float(0.5f));
        Float sp = Float.Sin(euler.Y * new Float(0.5f));
        Float cr = Float.Cos(euler.X * new Float(0.5f));
        Float sr = Float.Sin(euler.X * new Float(0.5f));

        return new Quaternion(
            sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy,
            cr * cp * sy - sr * sp * cy,
            cr * cp * cy + sr * sp * sy
        );
    }
    
    public Vector3 ToEuler()
    {
        Vector3 euler;

        // Roll (x-axis rotation)
        Float sinr_cosp = new Float(2) * (W * X + Y * Z);
        Float cosr_cosp = new Float(1) - new Float(2) * (X * X + Y * Y);
        euler.X = Float.Atan2(sinr_cosp, cosr_cosp);

        // Pitch (y-axis rotation)
        Float sinp = new Float(2) * (W * Y - Z * X);
        if (Float.Abs(sinp) >= new Float(1))
            euler.Y = Float.CopySign(Float.Pi / new Float(2), sinp); // use 90 degrees if out of range
        else
            euler.Y = Float.Atan(sinp / Float.Sqrt(new Float(1) - sinp * sinp));

        // Yaw (z-axis rotation)
        Float siny_cosp = new Float(2) * (W * Z + X * Y);
        Float cosy_cosp = new Float(1) - new Float(2) * (Y * Y + Z * Z);
        euler.Z = Float.Atan2(siny_cosp, cosy_cosp);

        return euler;
    }
}
