using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class EntityWorldTests : IDisposable
{
    private EntityWorld _world;

    public EntityWorldTests()
    {
        ComponentId.ClearMappings();
        ComponentId.RegisterAssembly(Assembly.GetExecutingAssembly());
        // Also need to register World component from ECS assembly
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        
        _world = new EntityWorld();
    }

    public void Dispose()
    {
        ComponentId.ClearMappings();
    }

    [Fact]
    public void Constructor_ShouldCreateWorldEntity()
    {
        _world.NextEntityId.Should().Be(1);
        _world.HasComponent<World>(new Entity(0)).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_ShouldIncrementId()
    {
        var e1 = _world.CreateEntity();
        var e2 = _world.CreateEntity();

        e1.Id.Should().Be(1);
        e2.Id.Should().Be(2);
        _world.NextEntityId.Should().Be(3);
    }

    [Fact]
    public void CreateEntity_WithComponent_ShouldAddComponent()
    {
        var entity = _world.CreateEntity<TestComponent1>();
        
        _world.HasComponent<TestComponent1>(entity).Should().BeTrue();
        _world.GetComponent<TestComponent1>(entity).Value.Should().Be(0);
    }

    [Fact]
    public void AddComponent_ShouldAddComponentAndMarkDirty()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new TestComponent1 { Value = 10 });

        _world.HasComponent<TestComponent1>(entity).Should().BeTrue();
        _world.GetComponent<TestComponent1>(entity).Value.Should().Be(10);
        
        _world.GetDirtyEntities().Should().Contain(entity.Id);
    }

    [Fact]
    public void RemoveComponent_ShouldRemoveComponentAndMarkDirty()
    {
        var entity = _world.CreateEntity<TestComponent1>();
        _world.ClearDirty(); // clear creation dirty

        _world.RemoveComponent<TestComponent1>(entity);

        _world.HasComponent<TestComponent1>(entity).Should().BeFalse();
        _world.GetDirtyEntities().Should().Contain(entity.Id);
    }
    
    [Fact]
    public void RemoveComponent_ShouldIgnore_IfEntityIdOutOfRange()
    {
        var entity = new Entity(9999);
        Action act = () => _world.RemoveComponent<TestComponent1>(entity);
        act.Should().NotThrow();
    }
    
    [Fact]
    public void TryGetComponent_ShouldReturnNull_WhenMissing()
    {
        var entity = _world.CreateEntity();
        
        var comp = _world.TryGetComponent<TestComponent1>(entity);
        
        comp.Should().BeNull();
    }
    
    [Fact]
    public void TryGetComponent_ShouldReturnValue_WhenPresent()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new TestComponent1 { Value = 5 });
        
        var comp = _world.TryGetComponent<TestComponent1>(entity);
        
        comp.Should().NotBeNull();
        comp!.Value.Value.Should().Be(5);
    }
    
    [Fact]
    public void HasComponent_ShouldReturnFalse_IfEntityIdOutOfRange()
    {
        _world.HasComponent<TestComponent1>(new Entity(9999)).Should().BeFalse();
    }

    [Fact]
    public void DeleteEntity_ShouldRemoveAllComponentsAndMarkDirty()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent(entity, new TestComponent1 { Value = 1 });
        _world.AddComponent(entity, new TestComponent2 { Value = 2f });
        _world.ClearDirty();

        _world.DeleteEntity(entity);

        _world.HasComponent<TestComponent1>(entity).Should().BeFalse();
        _world.HasComponent<TestComponent2>(entity).Should().BeFalse();
        _world.GetDirtyEntities().Should().Contain(entity.Id);
        
        // Ensure mask is cleared
        // Accessing internal state via reflection or just trust HasComponent?
        // Let's create a new entity with same ID (not possible with CreateEntity easily without reset)
        // But we can check filters.
        _world.Filter<TestComponent1>().Should().NotContain(entity);
    }

    [Fact]
    public void ResetComponents_ShouldClearEverything()
    {
        _world.CreateEntity<TestComponent1>();
        _world.ExternalState["test"] = new byte[1];
        
        _world.ResetComponents(clearCache: false);
        
        _world.NextEntityId.Should().Be(0); // It resets NextEntityId
        _world.ExternalState.Should().BeEmpty();
        _world.GetDirtyEntities().Should().BeEmpty();
        
        // Entity 0 (World) should also be gone/reset
        // Wait, constructor adds World entity. Reset clears it.
        // We probably need to re-add World entity if we want it, or Reset is raw.
        _world.HasComponent<World>(new Entity(0)).Should().BeFalse();
    }

    [Fact]
    public void ResetComponents_WithClearCache_ShouldClearArrays()
    {
        _world.CreateEntity<TestComponent1>();
        
        _world.ResetComponents(clearCache: true);
        
        _world.NextEntityId.Should().Be(0);
        // Can't easily verify internal arrays are null/cleared without reflection, but behavior should be same
    }

    [Fact]
    public void Filter_1_ShouldReturnMatchingEntities()
    {
        var e1 = _world.CreateEntity<TestComponent1>();
        var e2 = _world.CreateEntity<TestComponent2>();
        var e3 = _world.CreateEntity();
        _world.AddComponent(e3, new TestComponent1());
        _world.AddComponent(e3, new TestComponent2());

        var result = _world.Filter<TestComponent1>().ToList();

        result.Should().Contain(e1);
        result.Should().Contain(e3);
        result.Should().NotContain(e2);
    }

    [Fact]
    public void Filter_2_ShouldReturnMatchingEntities()
    {
        var e1 = _world.CreateEntity<TestComponent1>();
        var e2 = _world.CreateEntity<TestComponent2>();
        var e3 = _world.CreateEntity();
        _world.AddComponent(e3, new TestComponent1());
        _world.AddComponent(e3, new TestComponent2());

        var result = _world.Filter<TestComponent1, TestComponent2>().ToList();

        result.Should().Contain(e3);
        result.Should().NotContain(e1);
        result.Should().NotContain(e2);
    }

    [Fact]
    public void Filter_3_ShouldReturnMatchingEntities()
    {
        // Need a 3rd component
        // Assuming World is a component
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1());
        _world.AddComponent(e1, new TestComponent2());
        _world.AddComponent(e1, new World());

        var result = _world.Filter<TestComponent1, TestComponent2, World>().ToList();

        result.Should().Contain(e1);
    }
    
    [Fact]
    public void ForEach_1_ShouldIterate()
    {
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1 { Value = 10 });
        
        int count = 0;
        int sum = 0;
        _world.ForEach<TestComponent1>((ref TestComponent1 c1) =>
        {
            count++;
            sum += c1.Value;
            c1.Value += 1; // Verify ref
        });

        count.Should().Be(1);
        sum.Should().Be(10);
        _world.GetComponent<TestComponent1>(e1).Value.Should().Be(11);
    }

    [Fact]
    public void ForEach_2_ShouldIterate()
    {
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1 { Value = 10 });
        _world.AddComponent(e1, new TestComponent2 { Value = 20 });
        
        int count = 0;
        _world.ForEach<TestComponent1, TestComponent2>((ref TestComponent1 c1, ref TestComponent2 c2) =>
        {
            count++;
            c1.Value.Should().Be(10);
            c2.Value.Should().Be(20);
        });

        count.Should().Be(1);
    }
    
    [Fact]
    public void ForEach_Entity_1_ShouldIterate()
    {
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1 { Value = 10 });
        
        int count = 0;
        _world.ForEach<TestComponent1>((Entity e, ref TestComponent1 c1) =>
        {
            count++;
            e.Should().Be(e1);
            c1.Value.Should().Be(10);
        });

        count.Should().Be(1);
    }

    [Fact]
    public void ForEach_Entity_2_ShouldIterate()
    {
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1 { Value = 10 });
        _world.AddComponent(e1, new TestComponent2 { Value = 20 });
        
        int count = 0;
        _world.ForEach<TestComponent1, TestComponent2>((Entity e, ref TestComponent1 c1, ref TestComponent2 c2) =>
        {
            count++;
            e.Should().Be(e1);
        });

        count.Should().Be(1);
    }
    
    [Fact]
    public void ForEach_Entity_2_State_ShouldIterate()
    {
        var e1 = _world.CreateEntity();
        _world.AddComponent(e1, new TestComponent1 { Value = 10 });
        _world.AddComponent(e1, new TestComponent2 { Value = 20 });
        
        var state = new List<int>();
        
        _world.ForEach<TestComponent1, TestComponent2, List<int>>(state, (List<int> s, Entity e, ref TestComponent1 c1, ref TestComponent2 c2) =>
        {
            s.Add(e.Id);
        });

        state.Should().ContainSingle(x => x == e1.Id);
    }

    [Fact]
    public void GetComponentStore_ShouldReturnStore()
    {
        var e1 = _world.CreateEntity<TestComponent1>();

        var store = _world.GetComponentStore<TestComponent1>();

        store.Should().NotBeNull();
        store.Length.Should().BeGreaterThanOrEqualTo(e1.Id + 1);
        store.Get(e1.Id).Value.Should().Be(0);
    }
    
    [Fact]
    public void RegisterComponent_ShouldPreAllocate()
    {
        // New world to test pre-allocation
        var world = new EntityWorld();
        world.RegisterComponent<TestComponent1>();
        
        // Check if array exists (via GetRawArray)
        var store = world.GetComponentStore<TestComponent1>();
        store.Should().NotBeNull();
    }
    
    [Fact]
    public void Expansion_ShouldWork()
    {
        // Force expansion of entities
        int count = 300; // Default mask size is 256
        for(int i=0; i<count; i++)
        {
             _world.CreateEntity<TestComponent1>();
        }
        
        _world.NextEntityId.Should().Be(count + 1); // +1 for World entity
        _world.EntityMasks.Length.Should().BeGreaterThanOrEqualTo(count);
        
        // Force expansion of components
        var lastEntity = new Entity(count); 
        // Component array size starts at 32
        _world.GetComponent<TestComponent1>(lastEntity).Value = 999;
        
        _world.GetComponent<TestComponent1>(lastEntity).Value.Should().Be(999);
    }

    [Fact]
    public void World_Entity_ShouldBeZero()
    {
        World.Entity.Id.Should().Be(0);
    }

    [Fact]
    public void EntityWorld_ExpandTypeCapacity_ShouldWork()
    {
        var world = new EntityWorld();
        var typeField = typeof(EntityWorld).GetField("_componentTypes", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var sizeField = typeof(EntityWorld).GetField("_componentElementSizes", BindingFlags.Instance | BindingFlags.NonPublic);

        typeField.SetValue(world, new Type?[1]);
        arrayField.SetValue(world, new AlignedComponentStore?[1]);
        sizeField.SetValue(world, new int[1]);
        
        typeField.SetValue(world, new Type?[0]);
        arrayField.SetValue(world, new AlignedComponentStore?[0]);
        sizeField.SetValue(world, new int[0]);
        
        world.CreateEntity<TestComponent1>();
        
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EntityWorld_EnsureTypedCapacity_ShouldHandleNewTypes()
    {
        var world = new EntityWorld();
        var method = typeof(EntityWorld).GetMethod("EnsureTypedCapacity", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?.MakeGenericMethod(typeof(TestComponent1));
        method.Invoke(world, new object[] { ComponentId<TestComponent1>.IntId });
        world.GetComponentStore<TestComponent1>().Should().NotBeNull();
    }

    [Fact]
    public void EntityWorld_ResetComponents_ClearCache_ShouldWork()
    {
        var world = new EntityWorld();
        world.CreateEntity<TestComponent1>();
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        
        world.ResetComponents(clearCache: true);
        
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays[ComponentId<TestComponent1>.IntId].Should().BeNull();
    }

    [Fact]
    public void EntityWorld_RemoveComponent_ShouldSafeGuard()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        world.RemoveComponent<TestComponent1>(entity);
    }

    [Fact]
    public void EntityWorld_HasComponent_OutOfBounds()
    {
        var world = new EntityWorld();
        var entity = new Entity(99999);
        world.HasComponent<TestComponent1>(entity).Should().BeFalse();
    }

    [Fact]
    public void EntityWorld_DeleteEntity_OutOfBounds()
    {
        var world = new EntityWorld();
        var entity = new Entity(99999);
        Action act = () => world.DeleteEntity(entity);
        act.Should().NotThrow();
    }

    [Fact]
    public void EntityWorld_RemoveComponent_TypeIdOutOfRange()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        arrayField.SetValue(world, new AlignedComponentStore?[0]);
        world.RemoveComponent<TestComponent1>(e);
    }

    [Fact]
    public void EntityWorld_ExpandTypeCapacity_ExplicitCall()
    {
        var world = new EntityWorld();
        var typeField = typeof(EntityWorld).GetField("_componentTypes", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var sizeField = typeof(EntityWorld).GetField("_componentElementSizes", BindingFlags.Instance | BindingFlags.NonPublic);

        typeField.SetValue(world, new Type?[1]);
        arrayField.SetValue(world, new AlignedComponentStore?[1]);
        sizeField.SetValue(world, new int[1]);
        
        var method = typeof(EntityWorld).GetMethod("EnsureTypedCapacityInternal", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(world, new object[] { 5 });
        
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays.Length.Should().BeGreaterThan(5);
    }

    [Fact]
    public void EntityWorld_Expansion_ViaPublicApi()
    {
        var stableId = new StableComponentId(Guid.NewGuid());
        var denseId = new DenseComponentId(500); 
        ComponentId.RegisterType(typeof(TestComponentHigh), stableId);
        ComponentId.RegisterMapping(stableId, denseId);
        
        var world = new EntityWorld();
        world.RegisterComponent<TestComponentHigh>();
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays.Length.Should().BeGreaterThan(500);
    }

    [Fact]
    public void EntityWorld_DeleteEntity_ArrayNullCheck()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        world.EntityMasks[entity.Id].Set(10);
        world.DeleteEntity(entity);
        world.EntityMasks[entity.Id].IsSet(10).Should().BeFalse();
    }

    [Fact]
    public void EntityWorld_RemoveComponent_ArrayBounds()
    {
        var world = new EntityWorld();
        var entity = new Entity(900);
        world.EnsureEntityCapacity(900);
        world.RegisterComponent<TestComponent1>();
        world.EntityMasks[900].Set(ComponentId<TestComponent1>.IntId);
        world.RemoveComponent<TestComponent1>(entity);
        world.HasComponent<TestComponent1>(entity).Should().BeFalse();
    }

    [Fact]
    public void EntityWorld_EnsureTypedCapacity_Cached()
    {
        var world = new EntityWorld();
        var id = ComponentId<TestComponent1>.IntId;
        world.EnsureTypedCapacity<TestComponent1>(id);
        world.EnsureTypedCapacity<TestComponent1>(id);
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays[id].Should().NotBeNull();
    }

    [Fact]
    public void EntityWorld_Filter3_PartialMatch()
    {
        var world = new EntityWorld();
        var e1 = world.CreateEntity();
        world.AddComponent(e1, new TestComponent1());
        world.AddComponent(e1, new TestComponent2());
        
        var e2 = world.CreateEntity();
        world.AddComponent(e2, new TestComponent1());
        
        int count = 0;
        foreach(var e in world.Filter<TestComponent1, TestComponent2, TestComponent3>())
        {
            count++;
        }
        count.Should().Be(0);
    }

    [Fact]
    public void EntityWorld_AddComponent_OutOfBounds_TriggersMarkDirtyCheck()
    {
        var world = new EntityWorld();
        var entity = new Entity(9999);
        world.AddComponent(entity, new TestComponent1());
        world.HasComponent<TestComponent1>(entity).Should().BeTrue();
    }

    [Fact]
    public void EntityWorld_DeleteEntity_ArrayOutOfBounds()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        world.EnsureEntityCapacity(entity.Id);
        int highId = 100;
        world.EntityMasks[entity.Id].Set(highId);
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        arrayField.SetValue(world, new AlignedComponentStore?[10]);
        
        world.DeleteEntity(entity);
        world.EntityMasks[entity.Id].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void EntityWorld_RemoveComponent_WrongArrayType()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        var id = ComponentId<TestComponent1>.IntId;
        world.EnsureTypedCapacity<TestComponent1>(id);
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays[id] = null; // Simulate missing/corrupt store
        
        world.EnsureEntityCapacity(entity.Id);
        world.RemoveComponent<TestComponent1>(entity);
    }

    [Fact]
    public void EntityWorld_DeleteEntity_WrongArrayType()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        var id = ComponentId<TestComponent1>.IntId;
        world.EnsureTypedCapacity<TestComponent1>(id);
        world.EntityMasks[entity.Id].Set(id);
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays[id] = null; // Simulate missing/corrupt store
        
        world.DeleteEntity(entity);
    }

    [Fact]
    public void EntityWorld_DeleteEntity_ComponentArraySmallerThanLoop()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        arrayField.SetValue(world, new AlignedComponentStore?[10]);
        
        world.EntityMasks[entity.Id].Set(20);
        world.DeleteEntity(entity);
        world.EntityMasks[entity.Id].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void EntityWorld_MarkDirty_OutOfBounds()
    {
        var world = new EntityWorld();
        var method = typeof(EntityWorld).GetMethod("MarkDirty", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(world, new object[] { 300 });
    }

    [Fact]
    public void EntityWorld_MarkDirty_AlreadyDirty()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent1());
        world.AddComponent(entity, new TestComponent1 { Value = 2 });
        world.GetDirtyEntities().Should().Contain(entity.Id);
    }
}
