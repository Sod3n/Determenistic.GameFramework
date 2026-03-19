using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class BitMask128Tests
{
    [Fact]
    public void Set_ShouldSetBit()
    {
        var mask = new BitMask128();
        mask.Set(0);
        mask.Set(64);
        mask.Set(127);
        
        Assert.True(mask.IsSet(0));
        Assert.True(mask.IsSet(64));
        Assert.True(mask.IsSet(127));
        
        Assert.False(mask.IsSet(1));
        Assert.False(mask.IsSet(63));
        Assert.False(mask.IsSet(65));
    }

    [Fact]
    public void Unset_ShouldClearBit()
    {
        var mask = new BitMask128();
        mask.Set(10);
        Assert.True(mask.IsSet(10));
        
        mask.Unset(10);
        Assert.False(mask.IsSet(10));
    }

    [Fact]
    public void Clear_ShouldResetAll()
    {
        var mask = new BitMask128();
        mask.Set(0);
        mask.Set(100);
        
        mask.Clear();
        Assert.True(mask.IsEmpty);
        Assert.False(mask.IsSet(0));
        Assert.False(mask.IsSet(100));
    }

    [Fact]
    public void HasAll_ShouldWork()
    {
        var mask1 = new BitMask128();
        mask1.Set(1);
        mask1.Set(2);
        mask1.Set(70);

        var mask2 = new BitMask128();
        mask2.Set(1);
        mask2.Set(70);
        
        Assert.True(mask1.HasAll(mask2));
        Assert.False(mask2.HasAll(mask1)); // mask2 missing bit 2 (in part0)
        
        var mask3 = new BitMask128();
        mask3.Set(1); // Only in part0
        
        var mask4 = new BitMask128();
        mask4.Set(1);
        mask4.Set(70); // In part1
        
        // mask3 has 1. mask4 has 1 and 70.
        // mask4.HasAll(mask3) -> True.
        Assert.True(mask4.HasAll(mask3));
        
        // mask3.HasAll(mask4) -> Part0 matches (1 has 1), Part1 fails (0 does not have 70)
        Assert.False(mask3.HasAll(mask4));
    }

    [Fact]
    public void HasAny_ShouldWork()
    {
        var mask1 = new BitMask128();
        mask1.Set(1);
        
        var mask2 = new BitMask128();
        mask2.Set(2);
        
        var mask3 = new BitMask128();
        mask3.Set(1);
        
        Assert.False(mask1.HasAny(mask2));
        Assert.True(mask1.HasAny(mask3));
    }

    [Fact]
    public void StandardOverrides_ShouldWork()
    {
        var m1 = new BitMask128(); m1.Set(1);
        var m2 = new BitMask128(); m2.Set(1);
        var m3 = new BitMask128(); m3.Set(2);

        Assert.True(m1.Equals((object)m2));
        Assert.False(m1.Equals((object)m3));
        Assert.False(m1.Equals(null));
        Assert.False(m1.Equals("not a mask"));

        Assert.True(m1 == m2);
        Assert.True(m1 != m3);

        Assert.Equal(m1.GetHashCode(), m2.GetHashCode());
        Assert.NotEqual(m1.GetHashCode(), m3.GetHashCode());
    }

    [Fact]
    public void Unset_HighBit_ShouldWork()
    {
        var mask = new BitMask128();
        mask.Set(70);
        Assert.True(mask.IsSet(70));
        
        mask.Unset(70);
        Assert.False(mask.IsSet(70));
    }

    [Fact]
    public void IsEmpty_ShouldWork()
    {
        var mask = new BitMask128();
        Assert.True(mask.IsEmpty);
        
        mask.Set(0);
        Assert.False(mask.IsEmpty);
        
        mask.Clear();
        Assert.True(mask.IsEmpty);
        
        mask.Set(70);
        Assert.False(mask.IsEmpty);
        
        mask.Set(127);
        Assert.False(mask.IsEmpty);
        
        mask.Clear();
        Assert.True(mask.IsEmpty);
    }
    
    [Fact]
    public void Set_HighBit_ShouldWork()
    {
        var mask = new BitMask128();
        mask.Set(127);
        Assert.True(mask.IsSet(127));
    }
}
