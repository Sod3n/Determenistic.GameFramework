using System;
using System.Numerics;

#if NETSTANDARD2_1 || NETSTANDARD2_0
using WideInt = System.Numerics.BigInteger;
#else
using WideInt = System.Int128;
#endif

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
    public static Float operator -(Float a) => FromRaw(-a.RawValue);
    
    public static Float operator *(Float a, Float b)
    {
        WideInt result = ((WideInt)a.RawValue * b.RawValue) >> Shift;
        return FromRaw((long)result);
    }
    
    public static Float operator /(Float a, Float b)
    {
        if (b.RawValue == 0) throw new DivideByZeroException();
        WideInt result = ((WideInt)a.RawValue << Shift) / b.RawValue;
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
        WideInt v = (WideInt)val.RawValue << Shift;
        return FromRaw((long)ISqrt(v));
    }

    public static Float Abs(Float val) => FromRaw(Math.Abs(val.RawValue));
    public static Float Min(Float a, Float b) => a < b ? a : b;
    public static Float Max(Float a, Float b) => a > b ? a : b;
    public static Float Clamp(Float value, Float min, Float max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static Float Lerp(Float a, Float b, Float t)
    {
        t = Clamp(t, 0, 1);
        return a + (b - a) * t;
    }

    public static Float Floor(Float val)
    {
        long mask = ~((1L << Shift) - 1);
        return FromRaw(val.RawValue & mask);
    }

    public static Float Ceil(Float val)
    {
        long mask = (1L << Shift) - 1;
        if ((val.RawValue & mask) != 0)
        {
            return Floor(val) + 1;
        }
        return val;
    }

    public static Float Round(Float val)
    {
        return Floor(val + new Float(0.5f));
    }

    public static readonly Float Pi = new Float(3.14159274f);
    public static readonly Float TwoPi = new Float(6.28318548f);
    public static readonly Float HalfPi = new Float(1.57079637f);
    public static readonly Float Epsilon = FromRaw(1); // Smallest representable value

    public static Float Sin(Float val)
    {
        // Normalize to -PI to PI
        val %= TwoPi;
        if (val > Pi) val -= TwoPi;
        if (val < -Pi) val += TwoPi;

        // Bhaskara I's sine approximation formula:
        // sin(x) ≈ (16 * x * (π - |x|)) / (5 * π^2 - 4 * |x| * (π - |x|))
        // This is a good approximation for [-PI, PI]
        
        // For higher precision, we might want a Taylor series, but this is often "good enough" for games
        // and faster than many iterations.
        
        // Let's use a 5th order polynomial (Taylor series) for better precision near 0
        // sin(x) = x - x^3/6 + x^5/120
        
        Float x2 = val * val;
        Float x3 = x2 * val;
        Float x5 = x3 * x2;
        
        return val - x3 / new Float(6) + x5 / new Float(120);
    }

    public static Float Cos(Float val)
    {
        return Sin(val + HalfPi);
    }
    
    public static Float Atan2(Float y, Float x)
    {
        if (x == 0 && y == 0) return 0;
        
        // Simple approximation
        // https://pubs.opengroup.org/onlinepubs/009695399/functions/atan2.html
        // We can use a polynomial approximation for atan(z) where z = y/x
        
        if (x > 0) return Atan(y / x);
        if (x < 0 && y >= 0) return Atan(y / x) + Pi;
        if (x < 0 && y < 0) return Atan(y / x) - Pi;
        if (x == 0 && y > 0) return HalfPi;
        if (x == 0 && y < 0) return -HalfPi;
        
        return 0;
    }

    public static Float Atan(Float z)
    {
        // Polynomial approximation for Atan(z) for z in [-1, 1]
        // atan(z) ≈ z - z^3/3 + z^5/5 - z^7/7
        // If |z| > 1, use atan(z) = sgn(z) * PI/2 - atan(1/z)
        
        bool invert = Abs(z) > 1;
        if (invert) z = 1 / z;
        
        Float z2 = z * z;
        Float z3 = z2 * z;
        Float z5 = z3 * z2;
        Float z7 = z5 * z2;
        
        Float result = z - z3 / new Float(3) + z5 / new Float(5) - z7 / new Float(7);
        
        if (invert)
        {
            if (z > 0) return HalfPi - result;
            else return -HalfPi - result;
        }
        
        return result;
    }

    private static WideInt ISqrt(WideInt n)
    {
        if (n == 0) return 0;
        WideInt x = n;
        WideInt y = (x + 1) >> 1;
        while (y < x)
        {
            x = y;
            y = (x + n / x) >> 1;
        }
        return x;
    }
}
