using Xunit;
using Deterministic.GameFramework.Types;
using FixedMathSharp;

namespace Deterministic.GameFramework.Types.Tests;

public class Vector3Tests
{
    [Fact]
    public void Constructor_ShouldSetValues()
    {
        var v = new Vector3(1, 2, 3);
        Assert.Equal(new Float(1), v.X);
        Assert.Equal(new Float(2), v.Y);
        Assert.Equal(new Float(3), v.Z);
    }

    [Fact]
    public void Constants_ShouldBeCorrect()
    {
        Assert.Equal(new Vector3(0, 0, 0), Vector3.Zero);
        Assert.Equal(new Vector3(1, 1, 1), Vector3.One);
        Assert.Equal(new Vector3(1, 0, 0), Vector3.Right);
        Assert.Equal(new Vector3(-1, 0, 0), Vector3.Left);
        Assert.Equal(new Vector3(0, 1, 0), Vector3.Up);
        Assert.Equal(new Vector3(0, -1, 0), Vector3.Down);
        Assert.Equal(new Vector3(0, 0, 1), Vector3.Forward);
        Assert.Equal(new Vector3(0, 0, -1), Vector3.Back);
    }

    [Fact]
    public void Addition_ShouldWork()
    {
        var v1 = new Vector3(1, 2, 3);
        var v2 = new Vector3(4, 5, 6);
        var expected = new Vector3(5, 7, 9);
        
        Assert.Equal(expected, v1 + v2);
    }

    [Fact]
    public void DistanceSquared_ShouldWork()
    {
        var v1 = Vector3.Zero;
        var v2 = new Vector3(0, 3, 4); // Dist = 5, Sq = 25
        Assert.Equal(new Float(25), Vector3.DistanceSquared(v1, v2));
    }

    [Fact]
    public void Subtraction_ShouldWork()
    {
        var v1 = new Vector3(5, 7, 9);
        var v2 = new Vector3(4, 5, 6);
        var expected = new Vector3(1, 2, 3);
        
        Assert.Equal(expected, v1 - v2);
    }

    [Fact]
    public void Multiplication_Scalar_ShouldWork()
    {
        var v = new Vector3(1, 2, 3);
        var s = new Float(2);
        var expected = new Vector3(2, 4, 6);
        
        Assert.Equal(expected, v * s);
        Assert.Equal(expected, s * v);
    }

    [Fact]
    public void DotProduct_ShouldWork()
    {
        var v1 = new Vector3(1, 0, 0);
        var v2 = new Vector3(0, 1, 0);
        Assert.Equal(new Float(0), Vector3.Dot(v1, v2)); // Perpendicular

        var v3 = new Vector3(2, 0, 0);
        var v4 = new Vector3(3, 0, 0);
        Assert.Equal(new Float(6), Vector3.Dot(v3, v4)); // Parallel
    }

    [Fact]
    public void CrossProduct_ShouldWork()
    {
        var v1 = Vector3.Right;
        var v2 = Vector3.Up;
        
        // Right x Up = Forward (Right handed system? Standard Math usually z is up or forward depending on system. 
        // Here: 
        // X=1,0,0
        // Y=0,1,0
        // Cross:
        // x = 0*0 - 0*1 = 0
        // y = 0*0 - 1*0 = 0
        // z = 1*1 - 0*0 = 1
        // Result: 0,0,1 = Forward
        
        Assert.Equal(Vector3.Forward, Vector3.Cross(v1, v2));
    }

    [Fact]
    public void Magnitude_ShouldWork()
    {
        var v = new Vector3(3, 4, 0);
        Assert.Equal(new Float(5), v.Magnitude);
        Assert.Equal(new Float(25), v.SqrMagnitude);
    }

    [Fact]
    public void Normalized_ShouldWork()
    {
        var v = new Vector3(10, 0, 0);
        Assert.Equal(new Vector3(1, 0, 0), v.Normalized);
        
        var vZero = Vector3.Zero;
        Assert.Equal(Vector3.Zero, vZero.Normalized);
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        var v1 = new Vector3(0, 0, 0);
        var v2 = new Vector3(0, 3, 4);
        
        Assert.Equal(new Float(5), Vector3.Distance(v1, v2));
    }

    [Fact]
    public void Lerp_ShouldWork()
    {
        var start = Vector3.Zero;
        var end = new Vector3(10, 10, 10);
        
        Assert.Equal(new Vector3(5, 5, 5), Vector3.Lerp(start, end, new Float(0.5f)));
    }

    [Fact]
    public void MinMax_ShouldWork()
    {
        var v1 = new Vector3(1, 10, 5);
        var v2 = new Vector3(10, 1, 5);
        
        Assert.Equal(new Vector3(1, 1, 5), Vector3.Min(v1, v2));
        Assert.Equal(new Vector3(10, 10, 5), Vector3.Max(v1, v2));
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var v1 = new Vector3(1, 2, 3);
        var v2 = new Vector3(1, 2, 3);
        var v3 = new Vector3(1, 2, 4);

        Assert.True(v1.Equals((object)v2));
        Assert.False(v1.Equals((object)v3));
        Assert.False(v1.Equals(null));
        Assert.False(v1.Equals("not a vector"));

        Assert.True(v1 == v2);
        Assert.True(v1 != v3);

        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1.GetHashCode(), v3.GetHashCode());

        Assert.Contains("1", v1.ToString());
    }

    [Fact]
    public void ImplicitConversions_ShouldWork()
    {
        var v = new Vector3(1, 2, 3);
        Vector3d vd = v;
        Assert.Equal(1.0d, (double)vd.x);
        
        Vector3 vBack = vd;
        Assert.Equal(v, vBack);
    }

    [Fact]
    public void Equality_BranchCoverage_ShouldWork()
    {
        var v1 = new Vector3(1, 2, 3);
        var v2 = new Vector3(1, 9, 3); // Y differs
        var v3 = new Vector3(1, 2, 9); // Z differs
        var v4 = new Vector3(9, 2, 3); // X differs
        
        Assert.False(v1 == v2);
        Assert.False(v1 == v3);
        Assert.False(v1 == v4);
        
        Assert.False(v1.Equals(v2));
        Assert.False(v1.Equals(v3));
        Assert.False(v1.Equals(v4));
    }
}
