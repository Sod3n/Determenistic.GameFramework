using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class DeterministicRandomTests
{
    [Fact]
    public void SameSeed_ShouldProduceSameSequence()
    {
        var rng1 = new DeterministicRandom(12345);
        var rng2 = new DeterministicRandom(12345);
        
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(rng1.Next(), rng2.Next());
        }
    }

    [Fact]
    public void DifferentSeed_ShouldProduceDifferentSequence()
    {
        var rng1 = new DeterministicRandom(12345);
        var rng2 = new DeterministicRandom(67890);
        
        // It's statistically improbable they match immediately
        Assert.NotEqual(rng1.Next(), rng2.Next());
    }

    [Fact]
    public void NextInt_Range_ShouldBeRespected()
    {
        var rng = new DeterministicRandom(1);
        var min = 10;
        var max = 20;
        
        for (int i = 0; i < 100; i++)
        {
            var val = rng.NextInt(min, max);
            Assert.True(val >= min);
            Assert.True(val < max);
        }
    }

    [Fact]
    public void NextFloat_Range_ShouldBeRespected()
    {
        var rng = new DeterministicRandom(1);
        
        for (int i = 0; i < 100; i++)
        {
            var val = rng.NextFloat();
            Assert.True(val >= new Float(0));
            Assert.True(val <= new Float(1)); // Implementation allows 0..1 inclusive if treating max uint as 1.0? 
            // The code says: return Float.FromRaw(Next()); where Next returns uint.
            // RawValue of 1.0 is 1 << 32 (4294967296).
            // Uint max is 4294967295.
            // So it strictly returns < 1.0.
            Assert.True(val < new Float(1));
        }
    }

    [Fact]
    public void NextOnUnitCircle_ShouldBeNormalized()
    {
        var rng = new DeterministicRandom(1);
        for (int i = 0; i < 10; i++)
        {
            var v = rng.NextOnUnitCircle();
            var mag = Float.Sqrt(v.X * v.X + v.Y * v.Y);
            Assert.True(Float.Abs(mag - new Float(1)) < new Float(0.001f));
        }
    }

    [Fact]
    public void NextInsideUnitCircle_ShouldBeInside()
    {
        var rng = new DeterministicRandom(1);
        for (int i = 0; i < 10; i++)
        {
            var v = rng.NextInsideUnitCircle();
            var sqrMag = v.X * v.X + v.Y * v.Y;
            Assert.True(sqrMag <= new Float(1));
        }
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var r1 = new DeterministicRandom(1);
        var r2 = new DeterministicRandom(1);
        var r3 = new DeterministicRandom(2);

        Assert.True(r1.Equals((object)r2));
        Assert.False(r1.Equals((object)r3));
        Assert.False(r1.Equals(null));
        Assert.False(r1.Equals("not a random"));

        Assert.True(r1 == r2);
        Assert.True(r1 != r3);

        Assert.Equal(r1.GetHashCode(), r2.GetHashCode());
        Assert.NotEqual(r1.GetHashCode(), r3.GetHashCode());
    }

    [Fact]
    public void NextInt_MinGreaterThanMax_ShouldReturnMin()
    {
        var rng = new DeterministicRandom(1);
        Assert.Equal(10, rng.NextInt(10, 5));
    }

    [Fact]
    public void NextOnUnitCircle_StressTest_ShouldHitRejections()
    {
        var rng = new DeterministicRandom(123);
        // Run enough times to statistically guarantee we hit the rejection paths in the while(true) loop
        for (int i = 0; i < 100; i++)
        {
            var v = rng.NextOnUnitCircle();
            var mag = Float.Sqrt(v.X * v.X + v.Y * v.Y);
            Assert.True(Float.Abs(mag - new Float(1)) < new Float(0.001f));
        }
    }

    [Fact]
    public void NextInt_InvalidMax_ShouldReturnZero()
    {
        var rng = new DeterministicRandom(1);
        Assert.Equal(0, rng.NextInt(0));
        Assert.Equal(0, rng.NextInt(-10));
    }
}
