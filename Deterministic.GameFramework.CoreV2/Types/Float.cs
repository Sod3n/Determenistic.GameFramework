using System;

namespace Deterministic.GameFramework.CoreV2;

public struct Float : IParam, IEquatable<Float>, IComparable<Float>
{
    public long RawValue;
    public const int Shift = 32;
    public const long One = 1L << Shift;
    
    public Float(long rawValue)
    {
        RawValue = rawValue;
    }

    public Float(int value)
    {
        RawValue = (long)value << Shift;
    }

    public Float(float value)
    {
        RawValue = (long)(value * One);
    }

    public static Float FromRaw(long raw) => new Float(raw);

    public static implicit operator Float(int value) => new Float(value);
    public static implicit operator Float(float value) => new Float(value);
    public static explicit operator float(Float value) => (float)value.RawValue / One;
    public static explicit operator int(Float value) => (int)(value.RawValue >> Shift);

    public static Float operator +(Float a, Float b) => FromRaw(a.RawValue + b.RawValue);
    public static Float operator -(Float a, Float b) => FromRaw(a.RawValue - b.RawValue);
    
    public static Float operator *(Float a, Float b)
    {
        Int128 result = ((Int128)a.RawValue * b.RawValue) >> Shift;
        return FromRaw((long)result);
    }
    
    public static Float operator /(Float a, Float b)
    {
        if (b.RawValue == 0) throw new DivideByZeroException();
        Int128 result = ((Int128)a.RawValue << Shift) / b.RawValue;
        return FromRaw((long)result);
    }

    public static Float operator %(Float a, Float b) => FromRaw(a.RawValue % b.RawValue);

    public static bool operator ==(Float a, Float b) => a.RawValue == b.RawValue;
    public static bool operator !=(Float a, Float b) => a.RawValue != b.RawValue;
    public static bool operator >(Float a, Float b) => a.RawValue > b.RawValue;
    public static bool operator <(Float a, Float b) => a.RawValue < b.RawValue;
    public static bool operator >=(Float a, Float b) => a.RawValue >= b.RawValue;
    public static bool operator <=(Float a, Float b) => a.RawValue <= b.RawValue;

    public override bool Equals(object? obj) => obj is Float other && RawValue == other.RawValue;
    public bool Equals(Float other) => RawValue == other.RawValue;
    public override int GetHashCode() => RawValue.GetHashCode();
    public int CompareTo(Float other) => RawValue.CompareTo(other.RawValue);
    
    public override string ToString() => ((float)this).ToString("F5");

    public static Float Sqrt(Float val)
    {
        if (val.RawValue <= 0) return FromRaw(0);
        
        // Calculate Sqrt(x * 2^64) to get result in Q32.32 (x^0.5 * 2^32)
        Int128 v = (Int128)val.RawValue << Shift;
        return FromRaw((long)ISqrt(v));
    }

    private static Int128 ISqrt(Int128 n)
    {
        if (n == 0) return 0;
        Int128 x = n;
        Int128 y = (x + 1) >> 1;
        while (y < x)
        {
            x = y;
            y = (x + n / x) >> 1;
        }
        return x;
    }
}
