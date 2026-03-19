using Xunit;
using Deterministic.GameFramework.Types;
using FixedMathSharp;

namespace Deterministic.GameFramework.Types.Tests;

public class FloatTests
{
    [Fact]
    public void Constructor_FromInt_ShouldBePrecise()
    {
        var f = new Float(10);
        Assert.Equal(10, (int)f);
        Assert.Equal(10f, (float)f);
        Assert.Equal(10L << 32, f.RawValue);
    }

    [Fact]
    public void Constructor_FromFloat_ShouldBeApproximate()
    {
        var f = new Float(1.5f);
        Assert.Equal(1.5f, (float)f);
        // 1.5 in Q32.32 is 1.5 * 2^32 = 6442450944
        Assert.Equal(6442450944L, f.RawValue);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(1.5f, 2.5f, 4.0f)]
    [InlineData(-1, 1, 0)]
    public void Addition_ShouldWork(float a, float b, float expected)
    {
        var fa = new Float(a);
        var fb = new Float(b);
        var fExpected = new Float(expected);
        
        Assert.Equal(fExpected, fa + fb);
    }

    [Theory]
    [InlineData(5, 3, 2)]
    [InlineData(1.5f, 0.5f, 1.0f)]
    [InlineData(1, 5, -4)]
    public void Subtraction_ShouldWork(float a, float b, float expected)
    {
        var fa = new Float(a);
        var fb = new Float(b);
        var fExpected = new Float(expected);
        
        Assert.Equal(fExpected, fa - fb);
    }

    [Theory]
    [InlineData(2, 3, 6)]
    [InlineData(0.5f, 0.5f, 0.25f)]
    [InlineData(-2, 3, -6)]
    public void Multiplication_ShouldWork(float a, float b, float expected)
    {
        var fa = new Float(a);
        var fb = new Float(b);
        var fExpected = new Float(expected);
        
        Assert.Equal(fExpected, fa * fb);
    }

    [Theory]
    [InlineData(6, 2, 3)]
    [InlineData(1, 2, 0.5f)]
    [InlineData(-6, 2, -3)]
    public void Division_ShouldWork(float a, float b, float expected)
    {
        var fa = new Float(a);
        var fb = new Float(b);
        var fExpected = new Float(expected);
        
        // Allow for small precision error in division of floats due to conversion
        var result = fa / fb;
        var diff = Float.Abs(result - fExpected);
        
        Assert.True(diff < new Float(0.001f), $"Expected {expected}, got {result}");
    }

    [Fact]
    public void Constants_ShouldBeCorrect()
    {
        Assert.Equal(13493037703L, Float.Pi.RawValue);
        Assert.Equal(26986075406L, Float.TwoPi.RawValue);
        Assert.Equal(6746518851L, Float.HalfPi.RawValue);
    }

    [Fact]
    public void Sqrt_ShouldWork()
    {
        var val = new Float(16);
        Assert.Equal(new Float(4), Float.Sqrt(val));

        var val2 = new Float(2);
        // Sqrt(2) approx 1.41421356
        var result = Float.Sqrt(val2);
        Assert.Equal(1.41421356f, (float)result, 5);
    }

    [Fact]
    public void Lerp_ShouldWork()
    {
        var a = new Float(0);
        var b = new Float(10);
        
        Assert.Equal(new Float(0), Float.Lerp(a, b, new Float(0)));
        Assert.Equal(new Float(5), Float.Lerp(a, b, new Float(0.5f)));
        Assert.Equal(new Float(10), Float.Lerp(a, b, new Float(1)));
        Assert.Equal(new Float(10), Float.Lerp(a, b, new Float(2))); // Clamped
    }

    [Fact]
    public void Comparison_Operators_ShouldWork()
    {
        var a = new Float(1);
        var b = new Float(2);
        var c = new Float(1);

        Assert.True(a < b);
        Assert.True(b > a);
        Assert.True(a <= b);
        Assert.True(b >= a);
        Assert.True(a <= c);
        Assert.True(a >= c);
        Assert.True(a == c);
        Assert.True(a != b);
    }

    [Fact]
    public void UnaryNegation_ShouldWork()
    {
        var a = new Float(1);
        Assert.Equal(new Float(-1), -a);
    }

    [Fact]
    public void Modulus_ShouldWork()
    {
        var a = new Float(5);
        var b = new Float(2);
        Assert.Equal(new Float(1), a % b);
    }

    [Fact]
    public void Conversions_ShouldWork()
    {
        // Explicit
        Float f = new Float(1.5f);
        Assert.Equal(1, (int)f);
        Assert.Equal(1.5f, (float)f);

        // Implicit
        Float fInt = 10;
        Assert.Equal(new Float(10), fInt);
        
        Float fFloat = 1.5f;
        Assert.Equal(new Float(1.5f), fFloat);
        
        Fixed64 fixedVal = new Fixed64(2);
        Float fFixed = fixedVal;
        Assert.Equal(new Float(2), fFixed);
        
        Fixed64 back = fFixed;
        Assert.Equal(fixedVal, back);
    }

    [Fact]
    public void Rounding_Functions_ShouldWork()
    {
        var f = new Float(1.5f);
        Assert.Equal(new Float(1), Float.Floor(f));
        Assert.Equal(new Float(2), Float.Ceil(f));
        Assert.Equal(new Float(2), Float.Round(f));

        var f2 = new Float(1.4f);
        Assert.Equal(new Float(1), Float.Round(f2));
    }

    [Fact]
    public void MinMax_ShouldWork()
    {
        var a = new Float(1);
        var b = new Float(2);
        
        // a < b
        Assert.Equal(a, Float.Min(a, b));
        Assert.Equal(b, Float.Max(a, b));
        
        // b > a (swap)
        Assert.Equal(a, Float.Min(b, a));
        Assert.Equal(b, Float.Max(b, a));
        
        // Equal
        Assert.Equal(a, Float.Min(a, a));
        Assert.Equal(a, Float.Max(a, a));
    }

    [Fact]
    public void Sign_ShouldWork()
    {
        Assert.Equal(1, Float.Sign(new Float(10)));
        Assert.Equal(-1, Float.Sign(new Float(-10)));
        Assert.Equal(0, Float.Sign(new Float(0)));
    }

    [Fact]
    public void CopySign_ShouldWork()
    {
        var x = new Float(5);
        var y = new Float(-1);
        Assert.Equal(new Float(-5), Float.CopySign(x, y));
        
        var x2 = new Float(-5);
        var y2 = new Float(1);
        Assert.Equal(new Float(5), Float.CopySign(x2, y2));
    }

    [Fact]
    public void Trig_Functions_ShouldWork()
    {
        Assert.Equal(new Float(0), Float.Sin(new Float(0)));
        Assert.Equal(new Float(1), Float.Cos(new Float(0)));
        
        // Atan2(y, x)
        Assert.Equal(new Float(0), Float.Atan2(new Float(0), new Float(1)));
        
        // Atan(z)
        Assert.Equal(new Float(0), Float.Atan(new Float(0)));
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var a = new Float(1);
        var b = new Float(1);
        var c = new Float(2);

        Assert.True(a.Equals((object)b));
        Assert.False(a.Equals((object)c));
        Assert.False(a.Equals("not a float"));
        Assert.False(a.Equals(null));
        
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), c.GetHashCode());
        
        Assert.Equal(0, a.CompareTo(b));
        Assert.Equal(-1, a.CompareTo(c));
        Assert.Equal(1, c.CompareTo(a));
        
        Assert.NotEmpty(a.ToString());
    }

    [Fact]
    public void Sqrt_Negative_ShouldReturnZero()
    {
        var val = new Float(-1);
        Assert.Equal(new Float(0), Float.Sqrt(val));
    }
}
