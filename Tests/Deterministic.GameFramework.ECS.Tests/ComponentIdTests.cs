using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class ComponentIdTests : IDisposable
{
    public ComponentIdTests()
    {
        ComponentId.UnregisterAll();
        ComponentId.RegisterAssembly(Assembly.GetExecutingAssembly());
        ComponentId.RegisterAssembly(typeof(World).Assembly);
    }

    public void Dispose()
    {
        ComponentId.UnregisterAll();
    }

    [Fact]
    public void RegisterAssembly_ShouldRegisterComponentsWithStableIdAttribute()
    {
        // Act
        ComponentId.RegisterAssembly(Assembly.GetExecutingAssembly());

        // Assert
        var id1 = ComponentId.FromType<TestComponent1>();
        var id2 = ComponentId.FromType<TestComponent2>();

        id1.ToStable().Value.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        id2.ToStable().Value.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000002"));
    }

    [Fact]
    public void FromDense_ShouldThrow_WhenNotRegistered()
    {
        // Act
        Action act = () => ComponentId.FromDense(999);

        // Assert
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }
    
    [Fact]
    public void TryFromDense_ShouldReturnFalse_WhenNotRegistered()
    {
        // Act
        var result = ComponentId.TryFromDense(999, out var id);

        // Assert
        result.Should().BeFalse();
        id.Should().Be(default);
    }

    [Fact]
    public void FromStable_ShouldThrow_WhenNotRegistered()
    {
        // Act
        Action act = () => ComponentId.FromStable(Guid.NewGuid());

        // Assert
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void FromType_ShouldThrow_WhenNotRegistered()
    {
        // Act
        Action act = () => ComponentId.FromType<UnregisteredComponent>();

        // Assert
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void RegisterType_ShouldAssignDenseIdsSequentially()
    {
        // Clear default registrations from constructor
        ComponentId.UnregisterAll();

        // Arrange
        var stableId1 = new StableComponentId(Guid.NewGuid());
        var stableId2 = new StableComponentId(Guid.NewGuid());

        // Act
        ComponentId.RegisterType(typeof(TestComponent1), stableId1);
        ComponentId.RegisterType(typeof(TestComponent2), stableId2);

        // Assert
        ComponentId.FromType<TestComponent1>().ToDense().Value.Should().Be(0);
        ComponentId.FromType<TestComponent2>().ToDense().Value.Should().Be(1);
    }
    
    [Fact]
    public void RegisterType_ShouldThrow_WhenDuplicateStableId()
    {
        // Clear default registrations
        ComponentId.UnregisterAll();

        // Arrange
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);

        // Act
        Action act = () => ComponentId.RegisterType(typeof(TestComponent2), stableId);

        // Assert
        act.Should().Throw<Exception>().WithMessage("*Duplicated stable ids*");
    }

    [Fact]
    public void RegisterType_ShouldIgnore_WhenDuplicateRegistrationSameType()
    {
        // Clear default registrations
        ComponentId.UnregisterAll();

        // Arrange
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);

        // Act
        ComponentId.RegisterType(typeof(TestComponent1), stableId);

        // Assert
        // Should not throw
    }

    [Fact]
    public void TryGetDense_ShouldReturnCorrectDenseId()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var stableId = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var expectedDense = ComponentId.FromType<TestComponent1>().ToDense();

        // Act
        var found = ComponentId.TryGetDense(stableId, out var denseId);

        // Assert
        found.Should().BeTrue();
        denseId.Should().Be(expectedDense);
    }

    [Fact]
    public void TryGetStable_ShouldReturnCorrectStableId()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var stableId = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var denseId = ComponentId.FromType<TestComponent1>().ToDense();

        // Act
        var found = ComponentId.TryGetStable(denseId, out var resultStableId);

        // Assert
        found.Should().BeTrue();
        resultStableId.Should().Be(stableId);
    }
    
    [Fact]
    public void TryGetType_ShouldReturnCorrectType()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var stableId = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var denseId = ComponentId.FromType<TestComponent1>().ToDense();

        // Act
        var found = ComponentId.TryGetType(denseId, out var type);

        // Assert
        found.Should().BeTrue();
        type.Should().Be(typeof(TestComponent1));
    }
    
    [Fact]
    public void TryGetType_ShouldReturnFalse_WhenDenseIdUnknown()
    {
        // Act
        var found = ComponentId.TryGetType(new DenseComponentId(999), out var type);

        // Assert
        found.Should().BeFalse();
        type.Should().BeNull();
    }

    [Fact]
    public void RegisterMapping_ShouldUpdateMappings()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        
        var originalDense = ComponentId.FromType<TestComponent1>().ToDense();
        var newDense = new DenseComponentId(100);

        // Act
        ComponentId.RegisterMapping(stableId, newDense);

        // Assert
        ComponentId.FromType<TestComponent1>().ToDense().Should().Be(newDense);
        ComponentId.TryGetStable(newDense, out var mappedStable).Should().BeTrue();
        mappedStable.Should().Be(stableId);
    }
    
    [Fact]
    public void RegisterMapping_ShouldIgnore_WhenTypeUnknown()
    {
         // Arrange
         var stableId = new StableComponentId(Guid.NewGuid());
         
         // Act
         ComponentId.RegisterMapping(stableId, new DenseComponentId(100));
         
         // Assert
         ComponentId.TryGetDense(stableId, out _).Should().BeFalse();
    }

    [Fact]
    public void ComponentIdGeneric_ShouldUpdate_WhenMappingChanges()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        
        // Force static constructor
        var initialDense = ComponentId<TestComponent1>.DenseId;
        
        var newDense = new DenseComponentId(50);

        // Act
        ComponentId.RegisterMapping(stableId, newDense);

        // Assert
        ComponentId<TestComponent1>.DenseId.Should().Be(newDense);
        ComponentId<TestComponent1>.IntId.Should().Be(50);
    }
    
    [Fact]
    public void ComponentIdSerializer_ShouldReturnSortedMappings()
    {
        ComponentId.UnregisterAll();
        // Arrange
        var id1 = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var id2 = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        
        ComponentId.RegisterType(typeof(TestComponent1), id1);
        ComponentId.RegisterType(typeof(TestComponent2), id2);

        // Act
        var mappings = ComponentIdSerializer.GetMappingsSnapshot();

        // Assert
        mappings.Should().HaveCount(2);
        // Sorted by stable ID (GUID)
        mappings[0].Key.Value.Should().Be(id1.Value);
        mappings[1].Key.Value.Should().Be(id2.Value);
    }
    
    [Fact]
    public void Comparables_ShouldWork()
    {
        ComponentId.UnregisterAll();
        var d1 = new DenseComponentId(1);
        var d2 = new DenseComponentId(2);
        d1.CompareTo(d2).Should().BeNegative();
        
        var s1 = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var s2 = new StableComponentId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        s1.CompareTo(s2).Should().BeNegative();
        
        ComponentId.RegisterType(typeof(TestComponent1), s1);
        ComponentId.RegisterType(typeof(TestComponent2), s2);
        
        var c1 = ComponentId.FromType<TestComponent1>();
        var c2 = ComponentId.FromType<TestComponent2>();
        
        c1.CompareTo(c2).Should().BeNegative();
    }

    [Fact]
    public void ToString_ShouldBeFormatted()
    {
        var d = new DenseComponentId(123);
        d.ToString().Should().Be("Local(123)");
        
        var s = new StableComponentId(Guid.Empty);
        s.ToString().Should().Be($"Stable({Guid.Empty})");
    }
    
    [Fact]
    public void ImplicitOperators_ShouldWork()
    {
        DenseComponentId d = (DenseComponentId)10;
        int i = d;
        i.Should().Be(10);
        
        var g = Guid.NewGuid();
        StableComponentId s = (StableComponentId)g;
        Guid g2 = s;
        g2.Should().Be(g);
    }

    [Fact]
    public void FromDense_ShouldReturnCorrectId_WhenRegistered()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var dense = ComponentId.FromType<TestComponent1>().ToDense();

        var id = ComponentId.FromDense(dense.Value);
        
        id.ToStable().Should().Be(stableId);
    }

    [Fact]
    public void TryFromDense_ShouldReturnTrue_WhenRegistered()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var dense = ComponentId.FromType<TestComponent1>().ToDense();

        var result = ComponentId.TryFromDense(dense.Value, out var id);
        
        result.Should().BeTrue();
        id.ToStable().Should().Be(stableId);
    }

    [Fact]
    public void ComponentId_FromType_NonGeneric_ShouldThrow_WhenNotRegistered()
    {
        Action act = () => ComponentId.FromType(typeof(UnregisteredComponent));
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void ComponentId_ToType_ShouldReturnType()
    {
        var id = ComponentId.FromType<TestComponent1>();
        id.ToType().Should().Be(typeof(TestComponent1));
    }

    [Fact]
    public void ComponentId_TryFromDense_ShouldHandleInconsistency()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        var dense = ComponentId.FromType<TestComponent1>().ToDense();

        var field = typeof(ComponentId).GetField("StableToType", BindingFlags.Static | BindingFlags.NonPublic);
        var dict = (ConcurrentDictionary<StableComponentId, Type>)field.GetValue(null);
        dict.TryRemove(stableId, out _);

        var result = ComponentId.TryFromDense(dense.Value, out var id);
        result.Should().BeFalse();
        id.Should().Be(default);
    }

    [Fact]
    public void ComponentId_FromType_Success()
    {
        var id = ComponentId.FromType(typeof(TestComponent1));
        id.ToType().Should().Be(typeof(TestComponent1));
    }

    [Fact]
    public void RegisterAssembly_ShouldIgnoreInvalidTypes()
    {
        Action act1 = () => ComponentId.FromType<ComponentNoAttribute>();
        act1.Should().Throw<Exception>().WithMessage("*not registered*");
        
        Action act2 = () => ComponentId.FromType(typeof(ComponentClass));
        act2.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void ComponentId_FromStable_ShouldThrow_WhenNotLocal()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        
        var field = typeof(ComponentId).GetField("StableToDense", BindingFlags.Static | BindingFlags.NonPublic);
        var dict = (ConcurrentDictionary<StableComponentId, DenseComponentId>)field.GetValue(null);
        dict.TryRemove(stableId, out _);
        
        Action act = () => ComponentId.FromStable(stableId);
        act.Should().Throw<Exception>().WithMessage("*not registered as local*");
    }

    [Fact]
    public void ComponentId_FromTypeGeneric_ShouldThrow_WhenNotRegistered()
    {
        Action act = () => ComponentId.FromType<UnregisteredComponent>();
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void ComponentId_RegisterMapping_ShouldHandleMissingCache()
    {
        var type = typeof(TestComponent3);
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(type, stableId);
        
        var denseId = new DenseComponentId(123);
        ComponentId.RegisterMapping(stableId, denseId);
        
        ComponentId.TryGetDense(stableId, out var d).Should().BeTrue();
        d.Should().Be(denseId);
        ComponentId<TestComponent3>.DenseId.Should().Be(denseId);
    }

    [Fact]
    public void ComponentId_FromStable_ShouldThrow_WhenTypeUnknown()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        ComponentId.RegisterType(typeof(TestComponent1), stableId);
        
        var field = typeof(ComponentId).GetField("StableToType", BindingFlags.Static | BindingFlags.NonPublic);
        var dict = (ConcurrentDictionary<StableComponentId, Type>)field.GetValue(null);
        dict.TryRemove(stableId, out _);
        
        Action act = () => ComponentId.FromStable(stableId);
        act.Should().Throw<Exception>().WithMessage("*not registered*");
    }

    [Fact]
    public void ComponentId_StructMethods_Coverage()
    {
        var id = ComponentId.FromType<TestComponent1>();
        id.ToStable().Value.Should().NotBe(Guid.Empty);
        id.ToDense().Value.Should().BeGreaterThanOrEqualTo(0);
        id.ToType().Should().Be(typeof(TestComponent1));
        id.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ComponentId_TryFromDense_ExceptionFallback()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        var denseId = new DenseComponentId(999);
        
        var d2sField = typeof(ComponentId).GetField("DenseToStable", BindingFlags.Static | BindingFlags.NonPublic);
        var d2s = (ConcurrentDictionary<DenseComponentId, StableComponentId>)d2sField.GetValue(null);
        d2s[denseId] = stableId;
        
        bool result = ComponentId.TryFromDense(999, out var id);
        result.Should().BeFalse();
        id.Should().Be(default);
    }
}
