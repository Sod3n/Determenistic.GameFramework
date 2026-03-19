using System;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class EntityTests
{
    [Fact]
    public void Constructor_ShouldSetId()
    {
        var entity = new Entity(123);
        entity.Id.Should().Be(123);
    }

    [Fact]
    public void Null_ShouldHaveMinusOneId()
    {
        Entity.Null.Id.Should().Be(-1);
    }

    [Fact]
    public void ImplicitOperator_Int_ShouldReturnId()
    {
        Entity entity = new Entity(42);
        int id = entity;
        id.Should().Be(42);
    }

    [Fact]
    public void ExplicitOperator_Entity_ShouldCreateEntity()
    {
        int id = 42;
        Entity entity = (Entity)id;
        entity.Id.Should().Be(42);
    }

    [Fact]
    public void Equality_ShouldWork()
    {
        var e1 = new Entity(1);
        var e2 = new Entity(1);
        var e3 = new Entity(2);

        e1.Equals(e2).Should().BeTrue();
        e1.Equals((object)e2).Should().BeTrue();
        (e1 == e2).Should().BeTrue();
        (e1 != e3).Should().BeTrue();
        e1.Equals(e3).Should().BeFalse();
        
        e1.Equals(null).Should().BeFalse();
        e1.Equals("string").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_ShouldBeBasedOnId()
    {
        var e1 = new Entity(123);
        var e2 = new Entity(123);
        
        e1.GetHashCode().Should().Be(e2.GetHashCode());
        e1.GetHashCode().Should().Be(123.GetHashCode());
    }

    [Fact]
    public void CompareTo_ShouldCompareIds()
    {
        var e1 = new Entity(1);
        var e2 = new Entity(2);

        e1.CompareTo(e2).Should().BeNegative();
        e2.CompareTo(e1).Should().BePositive();
        e1.CompareTo(e1).Should().Be(0);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var entity = new Entity(99);
        entity.ToString().Should().Be("Entity(99)");
    }

    [Fact]
    public void Deconstruct_ShouldReturnId()
    {
        var entity = new Entity(55);
        entity.Deconstruct(out int id);
        id.Should().Be(55);
    }
}
