using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class QuaternionTests
{
    [Fact]
    public void Constructor_ShouldSetValues()
    {
        var q = new Quaternion(1, 2, 3, 4);
        Assert.Equal(new Float(1), q.X);
        Assert.Equal(new Float(2), q.Y);
        Assert.Equal(new Float(3), q.Z);
        Assert.Equal(new Float(4), q.W);
    }

    [Fact]
    public void Identity_ShouldBeCorrect()
    {
        var q = Quaternion.Identity;
        Assert.Equal(new Float(0), q.X);
        Assert.Equal(new Float(0), q.Y);
        Assert.Equal(new Float(0), q.Z);
        Assert.Equal(new Float(1), q.W);
    }

    [Fact]
    public void Multiplication_ShouldWork()
    {
        // Rotating 90 degrees around X axis
        // Euler: 90, 0, 0
        // Half angle: 45
        // Sin(45) = 0.7071..., Cos(45) = 0.7071...
        // Q = (sin(45), 0, 0, cos(45))
        
        var q = Quaternion.FromEuler(new Vector3(Float.HalfPi, 0, 0));
        var v = new Vector3(0, 1, 0); // Up
        
        // Rotating Up by 90 deg around Right (X) should give Forward (0, 0, 1) in right-handed system?
        // Let's check the implementation of * operator.
        // It casts to FixedQuaternion.
        
        var result = q * v;
        
        // Expected approx (0, 0, 1)
        Assert.True(Float.Abs(result.X) < new Float(0.01f));
        Assert.True(Float.Abs(result.Y) < new Float(0.01f));
        Assert.True(Float.Abs(result.Z - new Float(1)) < new Float(0.01f));
    }

    [Fact]
    public void FromEuler_ToEuler_RoundTrip()
    {
        var euler = new Vector3(0.5f, 0.3f, 0.1f);
        var q = Quaternion.FromEuler(euler);
        var result = q.ToEuler();
        
        Assert.True(Float.Abs(euler.X - result.X) < new Float(0.01f));
        Assert.True(Float.Abs(euler.Y - result.Y) < new Float(0.01f));
        Assert.True(Float.Abs(euler.Z - result.Z) < new Float(0.01f));
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var q1 = new Quaternion(1, 2, 3, 4);
        var q2 = new Quaternion(1, 2, 3, 4);
        var q3 = new Quaternion(1, 2, 3, 5);

        Assert.True(q1.Equals((object)q2));
        Assert.False(q1.Equals((object)q3));
        Assert.False(q1.Equals(null));
        Assert.False(q1.Equals("not a quaternion"));

        Assert.True(q1 == q2);
        Assert.True(q1 != q3);

        Assert.Equal(q1.GetHashCode(), q2.GetHashCode());
        Assert.NotEqual(q1.GetHashCode(), q3.GetHashCode());

        Assert.Contains("1", q1.ToString());
        Assert.Contains("2", q1.ToString());
    }

    [Fact]
    public void Quaternion_Multiplication_ShouldWork()
    {
        // q1 * q2
        var q1 = Quaternion.Identity;
        var q2 = new Quaternion(0.1f, 0.2f, 0.3f, 1.0f);
        Assert.Equal(q2, q1 * q2);
    }

    [Fact]
    public void Equality_BranchCoverage_ShouldWork()
    {
        var baseQ = new Quaternion(1, 2, 3, 4);
        var diffX = new Quaternion(9, 2, 3, 4);
        var diffY = new Quaternion(1, 9, 3, 4);
        var diffZ = new Quaternion(1, 2, 9, 4);
        var diffW = new Quaternion(1, 2, 3, 9);
        
        Assert.False(baseQ == diffX);
        Assert.False(baseQ == diffY);
        Assert.False(baseQ == diffZ);
        Assert.False(baseQ == diffW);
        
        Assert.False(baseQ.Equals(diffX));
        Assert.False(baseQ.Equals(diffY));
        Assert.False(baseQ.Equals(diffZ));
        Assert.False(baseQ.Equals(diffW));
    }

    [Fact]
    public void ToEuler_GimbalLock_ShouldHandle()
    {
        // Pitch 90 degrees (HalfPi)
        var euler = new Vector3(0, Float.HalfPi, 0);
        var q = Quaternion.FromEuler(euler);
        var result = q.ToEuler();
        
        // Should come back as (0, 90, 0)
        Assert.True(Float.Abs(result.X) < new Float(0.01f));
        Assert.True(Float.Abs(result.Y - Float.HalfPi) < new Float(0.01f));
        Assert.True(Float.Abs(result.Z) < new Float(0.01f));
        
        // Negative gimbal lock (-90)
        var eulerNeg = new Vector3(0, -Float.HalfPi, 0);
        var qNeg = Quaternion.FromEuler(eulerNeg);
        var resultNeg = qNeg.ToEuler();
        
        Assert.True(Float.Abs(resultNeg.Y - -Float.HalfPi) < new Float(0.01f));
    }
}
