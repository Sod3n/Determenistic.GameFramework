using System;
using Xunit;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Types.Tests;

public class Dictionary16DeterminismTests
{
    [Fact]
    public void Dictionary16_InsertionOrder_ShouldNotAffectEquality()
    {
        // Arrange
        var dict1 = new Dictionary16<FixedString32, int>();
        dict1.Add(new FixedString32("Key1"), 100);
        dict1.Add(new FixedString32("Key2"), 200);

        var dict2 = new Dictionary16<FixedString32, int>();
        dict2.Add(new FixedString32("Key2"), 200);
        dict2.Add(new FixedString32("Key1"), 100);

        // Act & Assert
        // Currently this fails because Dictionary16 uses List16 backing fields which depend on insertion order
        Assert.True(dict1.Equals(dict2), "Dictionaries with same content but different insertion order should be equal");
    }

    [Fact]
    public void Dictionary16_InsertionOrder_ShouldNotAffectHashCode()
    {
        // Arrange
        var dict1 = new Dictionary16<FixedString32, int>();
        dict1.Add(new FixedString32("Key1"), 100);
        dict1.Add(new FixedString32("Key2"), 200);

        var dict2 = new Dictionary16<FixedString32, int>();
        dict2.Add(new FixedString32("Key2"), 200);
        dict2.Add(new FixedString32("Key1"), 100);

        // Act & Assert
        // Currently this fails because HashCode depends on List16 order
        Assert.Equal(dict1.GetHashCode(), dict2.GetHashCode());
    }
}
