using System;
using System.Numerics;
using FixedMathSharp;

#if NETSTANDARD2_1 || NETSTANDARD2_0
using WideInt = System.Numerics.BigInteger;
#else
using WideInt = System.Int128;
#endif

namespace Deterministic.GameFramework.Types;

public struct Float : IParam, IEquatable<Float>, IComparable<Float>
{
    public Fixed64 Value;
    
    public long RawValue
    {
        get => Value.m_rawValue;
        set => Value = Fixed64.FromRaw(value);
    }
    
    public const int Shift = 32;
    public const long One = 1L << Shift;
    
    public Float(long rawValue)
    {
        Value = Fixed64.FromRaw(rawValue);
    }

    public Float(int value)
    {
        Value = new Fixed64(value);
    }

    public Float(float value)
    {
        Value = new Fixed64(value);
    }
    
    public Float(Fixed64 value)
    {
        Value = value;
    }

    public static Float FromRaw(long raw) => new Float(raw);

    public static implicit operator Float(int value) => new Float(value);
    public static implicit operator Float(float value) => new Float(value);
    public static implicit operator Float(Fixed64 value) => new Float(value);
    public static implicit operator Fixed64(Float value) => value.Value;
    
    public static explicit operator float(Float value) => (float)value.Value;
    public static explicit operator int(Float value) => (int)value.Value;

    public static Float operator +(Float a, Float b) => new Float(a.Value + b.Value);
    public static Float operator -(Float a, Float b) => new Float(a.Value - b.Value);
    public static Float operator -(Float a) => new Float(-a.Value);
    
    public static Float operator *(Float a, Float b) => new Float(a.Value * b.Value);
    public static Float operator /(Float a, Float b) => new Float(a.Value / b.Value);
    public static Float operator %(Float a, Float b) => new Float(a.Value % b.Value);

    public static bool operator ==(Float a, Float b) => a.Value == b.Value;
    public static bool operator !=(Float a, Float b) => a.Value != b.Value;
    public static bool operator >(Float a, Float b) => a.Value > b.Value;
    public static bool operator <(Float a, Float b) => a.Value < b.Value;
    public static bool operator >=(Float a, Float b) => a.Value >= b.Value;
    public static bool operator <=(Float a, Float b) => a.Value <= b.Value;

    public override bool Equals(object? obj) => obj is Float other && Value == other.Value;
    public bool Equals(Float other) => Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public int CompareTo(Float other) => Value.CompareTo(other.Value);
    
    public override string ToString() => Value.ToString();

    public static Float Sqrt(Float val)
    {
        if (val.Value < Fixed64.Zero) return new Float(0);
        return new Float(FixedMath.Sqrt(val.Value));
    }
    public static Float Abs(Float val) => new Float(FixedMath.Abs(val.Value));
    public static Float Min(Float a, Float b) => new Float(a.Value < b.Value ? a.Value : b.Value);
    public static Float Max(Float a, Float b) => new Float(a.Value > b.Value ? a.Value : b.Value);
    public static Float Clamp(Float value, Float min, Float max) => new Float(FixedMath.Clamp(value.Value, min.Value, max.Value));
    
    public static Float Lerp(Float a, Float b, Float t)
    {
        return a + (b - a) * Clamp(t, 0, 1);
    }

    public static Float Floor(Float val) => new Float(FixedMath.Floor(val.Value));
    public static Float Ceil(Float val) => new Float(FixedMath.Ceiling(val.Value));
    public static Float Round(Float val) => new Float(FixedMath.Round(val.Value));

    // Pi = 3.14159265358979323846
    // In Q32.32: 13493037703
    public static readonly Float Pi = new Float(13493037703L);
    
    // 2 * Pi
    // In Q32.32: 26986075406
    public static readonly Float TwoPi = new Float(26986075406L);
    
    // Pi / 2
    // In Q32.32: 6746518851
    public static readonly Float HalfPi = new Float(6746518851L);
    
    public static readonly Float Epsilon = new Float(1L);

    public static Float Sin(Float val) => new Float(FixedMath.Sin(val.Value));
    public static Float Cos(Float val) => new Float(FixedMath.Cos(val.Value));
    public static Float Atan2(Float y, Float x) => new Float(FixedMath.Atan2(y.Value, x.Value));
    public static Float Atan(Float z) => new Float(FixedMath.Atan(z.Value));

    public static Float CopySign(Float x, Float y)
    {
        // Copy sign of y to x
        if (y.Value.m_rawValue >= 0)
            return Abs(x);
        return -Abs(x);
    }
    
    public static int Sign(Float val)
    {
        if (val.Value.m_rawValue > 0) return 1;
        if (val.Value.m_rawValue < 0) return -1;
        return 0;
    }
}
