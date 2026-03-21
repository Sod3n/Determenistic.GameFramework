using System;
using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class List32Tests
{
    [Fact]
    public void Add_ShouldAddItem()
    {
        var list = new List32<int>();
        list.Add(10);
        list.Add(20);
        
        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(20, list[1]);
    }

    [Fact]
    public void Add_OverCapacity_ShouldThrow()
    {
        var list = new List32<int>();
        for (int i = 0; i < 32; i++)
        {
            list.Add(i);
        }
        
        Assert.Equal(32, list.Count);
        Assert.Throws<InvalidOperationException>(() => list.Add(32));
    }

    [Fact]
    public void Indexer_ShouldValidateRange()
    {
        var list = new List32<int>();
        list.Add(5);
        
        Assert.Throws<IndexOutOfRangeException>(() => list[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => list[1]);
        
        list[0] = 10;
        Assert.Equal(10, list[0]);
        
        // Test setter out of range validation
        Assert.Throws<IndexOutOfRangeException>(() => list[-1] = 5);
        Assert.Throws<IndexOutOfRangeException>(() => list[1] = 5);
    }

    [Fact]
    public void Clear_ShouldResetCount()
    {
        var list = new List32<int>();
        list.Add(1);
        list.Add(2);
        
        list.Clear();
        
        Assert.Equal(0, list.Count);
        Assert.Throws<IndexOutOfRangeException>(() => list[0]);
    }

    [Fact]
    public void Enumerator_ShouldIterate()
    {
        var list = new List32<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        
        int sum = 0;
        foreach (var item in list)
        {
            sum += item;
        }
        
        Assert.Equal(6, sum);

        // Test explicit interface implementation
        System.Collections.IEnumerable enumerable = list;
        var enumerator = enumerable.GetEnumerator();
        Assert.NotNull(enumerator);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
    }
    
    [Fact]
    public void Equality_ShouldWork()
    {
        var list1 = new List32<int>();
        list1.Add(1);
        list1.Add(2);

        var list2 = new List32<int>();
        list2.Add(1);
        list2.Add(2);
        
        var list3 = new List32<int>();
        list3.Add(1);
        list3.Add(3);
        
        Assert.True(list1.Equals(list2));
        Assert.False(list1.Equals(list3));
        Assert.True(list1 == list2);
        Assert.True(list1 != list3);
        
        Assert.True(list1.Equals((object)list2));
        Assert.False(list1.Equals(null));
        Assert.False(list1.Equals("not a list"));
        
        Assert.Equal(list1.GetHashCode(), list2.GetHashCode());
        Assert.NotEqual(list1.GetHashCode(), list3.GetHashCode());
        
        // Count mismatch
        var list4 = new List32<int>();
        list4.Add(1);
        Assert.False(list1.Equals(list4));
    }

    [Fact]
    public void FullCapacity_AccessAllIndices_ShouldWork()
    {
        var list = new List32<int>();
        for (int i = 0; i < 32; i++)
        {
            list.Add(i * 10);
        }
        
        for (int i = 0; i < 32; i++)
        {
            Assert.Equal(i * 10, list[i]);
            
            // Set
            list[i] = i * 20;
            Assert.Equal(i * 20, list[i]);
        }
    }

    [Fact]
    public void Unreachable_Switch_ShouldHandle_CorruptedState()
    {
        var list = new List32<int>();
        list.Count = 40; // Corrupt state
        
        // Index 35 is >= 0 and < Count (40), so it enters switch.
        // Switch only goes up to 31.
        // Should return default(int) which is 0.
        Assert.Equal(0, list[35]);
    }
}
