using Xunit;
using Deterministic.GameFramework.Types;
using FixedMathSharp;

namespace Deterministic.GameFramework.Types.Tests;

public class Vector2Tests
{
    [Fact]
    public void Constructor_ShouldSetValues()
    {
        var v = new Vector2(1, 2);
        Assert.Equal(new Float(1), v.X);
        Assert.Equal(new Float(2), v.Y);
    }

    [Fact]
    public void Constants_ShouldBeCorrect()
    {
        Assert.Equal(new Vector2(0, 0), Vector2.Zero);
        Assert.Equal(new Vector2(1, 1), Vector2.One);
        Assert.Equal(new Vector2(1, 0), Vector2.Right);
        Assert.Equal(new Vector2(-1, 0), Vector2.Left);
        Assert.Equal(new Vector2(0, 1), Vector2.Up);
        Assert.Equal(new Vector2(0, -1), Vector2.Down);
    }

    [Fact]
    public void Addition_ShouldWork()
    {
        var v1 = new Vector2(1, 2);
        var v2 = new Vector2(3, 4);
        Assert.Equal(new Vector2(4, 6), v1 + v2);
    }

    [Fact]
    public void Subtraction_ShouldWork()
    {
        var v1 = new Vector2(3, 5);
        var v2 = new Vector2(1, 2);
        Assert.Equal(new Vector2(2, 3), v1 - v2);
    }

    [Fact]
    public void Multiplication_Scalar_ShouldWork()
    {
        var v = new Vector2(2, 3);
        var s = new Float(2);
        Assert.Equal(new Vector2(4, 6), v * s);
        Assert.Equal(new Vector2(4, 6), s * v);
    }

    [Fact]
    public void Multiplication_ComponentWise_ShouldWork()
    {
        var v1 = new Vector2(2, 3);
        var v2 = new Vector2(4, 5);
        Assert.Equal(new Vector2(8, 15), v1 * v2);
    }

    [Fact]
    public void Division_Scalar_ShouldWork()
    {
        var v = new Vector2(4, 6);
        var s = new Float(2);
        Assert.Equal(new Vector2(2, 3), v / s);
    }

    [Fact]
    public void Equality_ShouldWork()
    {
        var v1 = new Vector2(1, 2);
        var v2 = new Vector2(1, 2);
        var v3 = new Vector2(1, 3);

        Assert.True(v1 == v2);
        Assert.False(v1 == v3);
        Assert.True(v1 != v3);
        Assert.True(v1.Equals(v2));
        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals(null));
        Assert.False(v1.Equals("not a vector"));
        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
    }

    [Fact]
    public void ImplicitConversions_ShouldWork()
    {
        var v = new Vector2(1, 2);
        Vector2d vd = v;
        Assert.Equal(1.0d, (double)vd.x);
        Assert.Equal(2.0d, (double)vd.y);

        Vector2 vBack = vd;
        Assert.Equal(v, vBack);
    }

    [Fact]
    public void ToString_ShouldWork()
    {
        var v = new Vector2(1, 2);
        Assert.Contains("1", v.ToString());
        Assert.Contains("2", v.ToString());
    }

    [Fact]
    public void Magnitude_ShouldWork()
    {
        var v = new Vector2(3, 4);
        Assert.Equal(new Float(5), v.Magnitude);
        Assert.Equal(new Float(25), v.SqrMagnitude);
    }

    [Fact]
    public void Normalized_ShouldWork()
    {
        var v = new Vector2(10, 0);
        Assert.Equal(new Vector2(1, 0), v.Normalized);
        Assert.Equal(Vector2.Zero, Vector2.Zero.Normalized);
    }

    [Fact]
    public void Dot_ShouldWork()
    {
        var v1 = new Vector2(1, 0);
        var v2 = new Vector2(0, 1);
        Assert.Equal(new Float(0), Vector2.Dot(v1, v2));

        var v3 = new Vector2(2, 0);
        var v4 = new Vector2(3, 0);
        Assert.Equal(new Float(6), Vector2.Dot(v3, v4));
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        var v1 = new Vector2(0, 0);
        var v2 = new Vector2(3, 4);
        Assert.Equal(new Float(5), Vector2.Distance(v1, v2));
        Assert.Equal(new Float(25), Vector2.DistanceSquared(v1, v2));
    }

    [Fact]
    public void Lerp_ShouldWork()
    {
        var v1 = Vector2.Zero;
        var v2 = new Vector2(10, 10);
        Assert.Equal(new Vector2(5, 5), Vector2.Lerp(v1, v2, new Float(0.5f)));
    }

    [Fact]
    public void MinMax_ShouldWork()
    {
        var v1 = new Vector2(1, 10);
        var v2 = new Vector2(10, 1);
        
        Assert.Equal(new Vector2(1, 1), Vector2.Min(v1, v2));
        Assert.Equal(new Vector2(10, 10), Vector2.Max(v1, v2));
    }

    [Fact]
    public void Equality_BranchCoverage_ShouldWork()
    {
        var baseV = new Vector2(1, 2);
        var diffX = new Vector2(9, 2);
        var diffY = new Vector2(1, 9);
        
        Assert.False(baseV == diffX);
        Assert.False(baseV == diffY);
        
        Assert.False(baseV.Equals(diffX));
        Assert.False(baseV.Equals(diffY));
    }

    [Fact]
    public void Rotate_ShouldWork()
    {
        var v = Vector2.Right; // (1, 0)
        v.Rotate(Float.HalfPi); // 90 deg -> (0, 1) approx
        
        // Relax epsilon slightly for trig operations
        var epsilon = new Float(0.01f);
        Assert.True(Float.Abs(v.X) < epsilon, $"Expected X ~ 0, got {v.X}");
        Assert.True(Float.Abs(v.Y - new Float(1)) < epsilon, $"Expected Y ~ 1, got {v.Y}");
    }

    [Fact]
    public void ToAngle_ShouldWork()
    {
        var v = Vector2.Right;
        Assert.Equal(new Float(0), v.ToAngle());
        
        var vUp = Vector2.Up;
        // Atan2(1, 0) -> Pi/2
        Assert.True(Float.Abs(vUp.ToAngle() - Float.HalfPi) < new Float(0.001f));
    }
}
