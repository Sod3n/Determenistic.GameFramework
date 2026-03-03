using System;

namespace Deterministic.GameFramework.CoreV2;

/// <summary>
/// A deterministic Random Number Generator (RNG) component using the Xoshiro128** algorithm.
/// This struct is blittable and can be stored directly in the GlobalState.
/// </summary>
[NetworkId("00000000-0000-0000-0000-000000000384")]
public struct DeterministicRandom : IComponent, IEquatable<DeterministicRandom>
{
    // 128-bit state
    public uint S0;
    public uint S1;
    public uint S2;
    public uint S3;

    public DeterministicRandom(uint seed)
    {
        // Initialize state using SplitMix32-like expansion to ensure good distribution from a simple seed
        S0 = seed;
        S1 = seed ^ 0x9E3779B9;
        S2 = (seed << 1) | (seed >> 31);
        S3 = (seed >> 1) | (seed << 31);
        
        // Warm up the state
        for (int i = 0; i < 20; i++)
        {
            Next();
        }
    }
    
    public bool Equals(DeterministicRandom other)
    {
        return S0 == other.S0 && S1 == other.S1 && S2 == other.S2 && S3 == other.S3;
    }
    
    public override bool Equals(object? obj) => obj is DeterministicRandom other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(S0, S1, S2, S3);
    
    public static bool operator ==(DeterministicRandom a, DeterministicRandom b) => a.Equals(b);
    public static bool operator !=(DeterministicRandom a, DeterministicRandom b) => !a.Equals(b);

    /// <summary>
    /// Returns the next random uint.
    /// </summary>
    public uint Next()
    {
        uint result = Rotl(S1 * 5, 7) * 9;
        uint t = S1 << 9;

        S2 ^= S0;
        S3 ^= S1;
        S1 ^= S2;
        S0 ^= S3;

        S2 ^= t;
        S3 = Rotl(S3, 11);

        return result;
    }

    /// <summary>
    /// Returns a random Int between 0 (inclusive) and max (exclusive).
    /// </summary>
    public Int NextInt(Int max)
    {
        if (max <= 0) return 0;
        return new Int((int)(Next() % (uint)max.Value));
    }

    /// <summary>
    /// Returns a random Int between min (inclusive) and max (exclusive).
    /// </summary>
    public Int NextInt(Int min, Int max)
    {
        if (min >= max) return min;
        return min + NextInt(max - min);
    }

    /// <summary>
    /// Returns a random Float between 0.0 (inclusive) and 1.0 (inclusive).
    /// </summary>
    public Float NextFloat()
    {
        // Generate random integer in range [0, One]
        // Float.One is 1 << 32.
        // We can just take the Next() uint and shift/mask it to fit.
        // Ideally we want 32 bits of precision.
        
        // Approach: standard float generation is (rand >> 9) * 1.0/2^23
        // For our fixed point:
        // We want a value v where 0 <= v <= One
        // Next() returns 0..uint.MaxValue.
        // We can normalize: (Next() * One) / uint.MaxValue
        // But that might overflow intermediate.
        
        // Simpler: Just take top bits to represent fraction
        // Our Float is Q32.32.
        // RawValue = (long)value
        // 1.0 = 1 << 32
        
        // We can treat Next() as the fractional part.
        // But Next() is 32-bit.
        // If we just use Next() as the lower 32 bits of RawValue, we get a number between 0 and 0.9999...
        // RawValue = (long)Next(); -> 0 .. 2^32-1
        // This is exactly [0, 1) in Q32.32 representation IF the integer part is 0.
        
        // Valid range for [0, 1] is RawValue in [0, 4294967296]
        // Next() returns [0, 4294967295]
        // So putting it directly in RawValue gives [0, 1) (exclusive 1.0).
        // This is usually what we want for random floats.
        
        return Float.FromRaw(Next());
    }

    /// <summary>
    /// Returns a random Float between min and max.
    /// </summary>
    public Float NextFloat(Float min, Float max)
    {
        return Float.Lerp(min, max, NextFloat());
    }

    /// <summary>
    /// Returns a random unit vector on the circle.
    /// </summary>
    public Vector2 NextOnUnitCircle()
    {
        // Deterministic trig is hard without a lookup table or expensive series.
        // Rejection sampling is easier and deterministic.
        
        while (true)
        {
            // Range [-1, 1]
            Float x = NextFloat(new Float(-1), new Float(1));
            Float y = NextFloat(new Float(-1), new Float(1));
            
            Float sqrMag = x * x + y * y;
            if (sqrMag > (Float)0 && sqrMag <= (Float)1)
            {
                Float mag = Float.Sqrt(sqrMag);
                return new Vector2(x / mag, y / mag);
            }
        }
    }
    
    /// <summary>
    /// Returns a random vector inside the unit circle.
    /// </summary>
    public Vector2 NextInsideUnitCircle()
    {
        while (true)
        {
            Float x = NextFloat(new Float(-1), new Float(1));
            Float y = NextFloat(new Float(-1), new Float(1));
            
            if (x * x + y * y <= (Float)1)
            {
                return new Vector2(x, y);
            }
        }
    }

    private static uint Rotl(uint x, int k)
    {
        return (x << k) | (x >> (32 - k));
    }
}
