using System;
using System.Collections.Generic;
using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class Dictionary64Tests
{
    [Fact]
    public void Add_ShouldAddItem()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        dict.Add(2, 20);
        
        Assert.Equal(2, dict.Count);
        Assert.Equal(10, dict[1]);
        Assert.Equal(20, dict[2]);
    }

    [Fact]
    public void Add_DuplicateKey_ShouldThrow()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        
        Assert.Throws<ArgumentException>(() => dict.Add(1, 20));
    }

    [Fact]
    public void Add_OverCapacity_ShouldThrow()
    {
        var dict = new Dictionary64<int, int>();
        for (int i = 0; i < 64; i++)
        {
            dict.Add(i, i * 10);
        }
        
        Assert.Equal(64, dict.Count);
        Assert.Throws<InvalidOperationException>(() => dict.Add(64, 640));
    }

    [Fact]
    public void Indexer_Get_ShouldReturnItem()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        
        Assert.Equal(10, dict[1]);
    }

    [Fact]
    public void Indexer_Get_NotFound_ShouldThrow()
    {
        var dict = new Dictionary64<int, int>();
        Assert.Throws<KeyNotFoundException>(() => dict[1]);
    }

    [Fact]
    public void Indexer_Set_Existing_ShouldUpdate()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        dict[1] = 20;
        
        Assert.Equal(20, dict[1]);
        Assert.Equal(1, dict.Count);
    }

    [Fact]
    public void Indexer_Set_New_ShouldAdd()
    {
        var dict = new Dictionary64<int, int>();
        dict[1] = 10;
        
        Assert.Equal(10, dict[1]);
        Assert.Equal(1, dict.Count);
    }

    [Fact]
    public void TryGetValue_ShouldReturnTrueIfFound()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        
        Assert.True(dict.TryGetValue(1, out int value));
        Assert.Equal(10, value);
    }

    [Fact]
    public void TryGetValue_ShouldReturnFalseIfNotFound()
    {
        var dict = new Dictionary64<int, int>();
        
        Assert.False(dict.TryGetValue(1, out int value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void ContainsKey_ShouldReturnTrueIfFound()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        
        Assert.True(dict.ContainsKey(1));
    }

    [Fact]
    public void ContainsKey_ShouldReturnFalseIfNotFound()
    {
        var dict = new Dictionary64<int, int>();
        
        Assert.False(dict.ContainsKey(1));
    }

    [Fact]
    public void Clear_ShouldResetCount()
    {
        var dict = new Dictionary64<int, int>();
        dict.Add(1, 10);
        dict.Clear();
        
        Assert.Equal(0, dict.Count);
        Assert.False(dict.ContainsKey(1));
    }

    [Fact]
    public void Equality_ShouldWork()
    {
        var dict1 = new Dictionary64<int, int>();
        dict1.Add(1, 10);
        dict1.Add(2, 20);

        var dict2 = new Dictionary64<int, int>();
        dict2.Add(1, 10);
        dict2.Add(2, 20);
        
        var dict3 = new Dictionary64<int, int>();
        dict3.Add(1, 10);
        dict3.Add(2, 30); // Different value
        
        var dict4 = new Dictionary64<int, int>();
        dict4.Add(1, 10); // Different count
        
        Assert.True(dict1.Equals(dict2));
        Assert.False(dict1.Equals(dict3));
        Assert.False(dict1.Equals(dict4));
        
        Assert.True(dict1 == dict2);
        Assert.True(dict1 != dict3);
        
        Assert.Equal(dict1.GetHashCode(), dict2.GetHashCode());
        Assert.NotEqual(dict1.GetHashCode(), dict3.GetHashCode());
    }
}
