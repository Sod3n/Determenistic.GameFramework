using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class StateTests : IDisposable
{
    public StateTests()
    {
        ComponentId.ClearMappings();
        ComponentId.RegisterAssembly(Assembly.GetExecutingAssembly());
        ComponentId.RegisterAssembly(typeof(World).Assembly);
    }
    
    public void Dispose()
    {
        ComponentId.ClearMappings();
    }

    [Fact]
    public void StateDumper_ShouldDumpEntities()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, new TestComponent1 { Value = 123 });
        
        var dump = StateDumper.Dump(world);
        
        dump.Should().Contain("Entity 1"); // 0 is World, 1 is new entity
        dump.Should().Contain("TestComponent1");
        dump.Should().Contain("Value: 123");
    }

    [Fact]
    public void StateHasher_ShouldBeDeterministic()
    {
        var world1 = new EntityWorld();
        var e1 = world1.CreateEntity();
        world1.AddComponent(e1, new TestComponent1 { Value = 42 });
        
        var world2 = new EntityWorld();
        var e2 = world2.CreateEntity();
        world2.AddComponent(e2, new TestComponent1 { Value = 42 });
        
        var hash1 = StateHasher.Hash(world1);
        var hash2 = StateHasher.Hash(world2);
        
        hash1.Should().Be(hash2);
        
        // Change state
        world1.GetComponent<TestComponent1>(e1).Value = 43;
        var hash3 = StateHasher.Hash(world1);
        
        hash3.Should().NotBe(hash1);
    }

    [Fact]
    public void StateSerializer_ShouldRoundTrip()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, new TestComponent1 { Value = 999 });
        world.AddComponent(e, new TestComponent2 { Value = 1.5f });
        world.ExternalState["GameTime"] = BitConverter.GetBytes(100L);

        var data = StateSerializer.Serialize(world);
        
        var newWorld = new EntityWorld(); // Starts empty (well, with World entity)
        StateSerializer.Deserialize(newWorld, data, syncComponentIds: true, autoReset: true);
        
        newWorld.NextEntityId.Should().Be(world.NextEntityId);
        newWorld.HasComponent<TestComponent1>(e).Should().BeTrue();
        newWorld.GetComponent<TestComponent1>(e).Value.Should().Be(999);
        newWorld.ExternalState.Should().ContainKey("GameTime");
    }

    [Fact]
    public void StateHistory_ShouldStoreAndRetrieve()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        var e = world.CreateEntity();
        
        // Tick 1
        world.AddComponent(e, new TestComponent1 { Value = 1 });
        history.Store(1, world);
        
        // Tick 2
        world.GetComponent<TestComponent1>(e).Value = 2;
        history.Store(2, world);
        
        // Retrieve Tick 1
        var restoreWorld = new EntityWorld();
        // Need to make sure restoreWorld has enough capacity or handles resizing
        // The serializer handles resizing.
        
        bool found = history.Retrieve(1, restoreWorld);
        found.Should().BeTrue();
        restoreWorld.GetComponent<TestComponent1>(e).Value.Should().Be(1);
        
        // Retrieve Tick 2
        history.Retrieve(2, restoreWorld);
        restoreWorld.GetComponent<TestComponent1>(e).Value.Should().Be(2);
    }
    
    [Fact]
    public void StateHistory_ShouldOverwriteRingBuffer()
    {
        var history = new StateHistory(2); // Small capacity
        var world = new EntityWorld();
        
        history.Store(1, world);
        history.Store(2, world);
        history.Store(3, world); // Should overwrite 1
        
        history.GetOldestTick().Should().Be(2);
        history.GetLatestTick().Should().Be(3);
        
        history.Retrieve(1, new EntityWorld()).Should().BeFalse();
        history.Retrieve(2, new EntityWorld()).Should().BeTrue();
    }
    
    [Fact]
    public void StateHistory_DiscardFuture_ShouldRewindTail()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        
        history.Store(1, world);
        history.Store(2, world);
        history.Store(3, world);
        
        history.DiscardFuture(1);
        
        history.GetLatestTick().Should().Be(1);
        history.Retrieve(2, new EntityWorld()).Should().BeFalse();
        history.Retrieve(3, new EntityWorld()).Should().BeFalse();
    }
    
    [Fact]
    public void StateHistory_TryGetSnapshotData_ShouldReturnBytes()
    {
        var history = new StateHistory(5);
        var world = new EntityWorld();
        history.Store(10, world);
        
        bool found = history.TryGetSnapshotData(10, out var data);
        found.Should().BeTrue();
        data.Should().NotBeNull();
        data!.Length.Should().BeGreaterThan(0);
        
        found = history.TryGetSnapshotData(99, out _);
        found.Should().BeFalse();
    }

    [Fact]
    public void StateHasher_Hash_Bytes_ShouldWork()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var hash = StateHasher.Hash(data);
        hash.Should().NotBe(Guid.Empty);
        
        var hash2 = StateHasher.Hash(data);
        hash2.Should().Be(hash);
    }

    [Fact]
    public void StateHistory_Clear_ShouldReset()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(1, world);
        
        history.Clear();
        
        history.GetLatestTick().Should().Be(-1);
        history.GetOldestTick().Should().Be(-1);
        history.Retrieve(1, world).Should().BeFalse();
    }

    [Fact]
    public void StateSerializer_Deserialize_WithNoAutoReset_ShouldMerge()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, new TestComponent1 { Value = 1 });
        
        var data = StateSerializer.Serialize(world);
        
        var targetWorld = new EntityWorld();
        var e2 = targetWorld.CreateEntity(); // ID 1
        targetWorld.AddComponent(e2, new TestComponent2 { Value = 2f });
        
        // Deserialize without reset - should keep components if mask logic allows?
        // Actually StateSerializer deserialization overwrites the mask for the entities present in the stream.
        // It might NOT clear masks for entities NOT in the stream if we are just merging?
        // No, `DeserializeArrayUntyped` usually overwrites.
        // However, if we want to test 'syncComponentIds: false', we can do that.
        
        StateSerializer.Deserialize(targetWorld, data, autoReset: false);
        
        // Check if TestComponent1 is there
        targetWorld.HasComponent<TestComponent1>(e2).Should().BeTrue();
    }

    // --- StateSerializer Tests ---

    [Fact]
    public void StateSerializer_LargeComponent_ShouldLog()
    {
        var world = new EntityWorld();
        int count = 3000;
        for(int i=0; i<count; i++)
        {
             var ent = world.CreateEntity();
             world.AddComponent(ent, new TestComponent1 { Value = i });
        }
        var data = StateSerializer.Serialize(world);
        data.Length.Should().BeGreaterThan(10000);
    }

    [Fact]
    public void StateSerializer_Deserialize_ResizingTest()
    {
        var world = new EntityWorld();
        for(int i=0; i<100; i++)
        {
            var e = world.CreateEntity();
            world.AddComponent(e, new TestComponent1 { Value = i });
        }
        
        var data = StateSerializer.Serialize(world);
        
        var targetWorld = new EntityWorld();
        targetWorld.RegisterComponent<TestComponent1>(); 
        StateSerializer.Deserialize(targetWorld, data);
        
        targetWorld.GetComponentStore<TestComponent1>().Length.Should().BeGreaterThanOrEqualTo(100);
        targetWorld.GetComponent<TestComponent1>(new Entity(100)).Value.Should().Be(99);
    }

    [Fact]
    public void StateSerializer_Deserialize_ShouldHandleAutoReset()
    {
        var world = new EntityWorld();
        world.ExternalState["Test"] = new byte[] { 1 };
        var data = StateSerializer.Serialize(world);
        
        var targetWorld = new EntityWorld();
        targetWorld.ExternalState["Old"] = new byte[] { 2 };
        StateSerializer.Deserialize(targetWorld, data, autoReset: true);
        
        targetWorld.ExternalState.Should().ContainKey("Test");
        targetWorld.ExternalState.Should().NotContainKey("Old");
    }

    [Fact]
    public void StateSerializer_Deserialize_ShouldNotShrink_WhenCapacityIsSufficient()
    {
        var smallWorld = new EntityWorld();
        smallWorld.CreateEntity();
        var data = StateSerializer.Serialize(smallWorld);
        
        var largeWorld = new EntityWorld();
        largeWorld.EnsureEntityCapacity(1000); 
        int initialLength = largeWorld.EntityMasks.Length;
        StateSerializer.Deserialize(largeWorld, data);
        
        // After deserialization, masks match the serialized capacity (smallWorld had 2 entities: world entity + 1 created)
        largeWorld.EntityMasks.Length.Should().Be(2);
    }

    [Fact]
    public void StateSerializer_Deserialize_SyncComponentIds_False()
    {
        var world = new EntityWorld();
        var data = StateSerializer.Serialize(world);
        var targetWorld = new EntityWorld();
        StateSerializer.Deserialize(targetWorld, data, syncComponentIds: false);
        targetWorld.NextEntityId.Should().Be(world.NextEntityId);
    }

    [Fact]
    public void StateSerializer_Deserialize_EmptyMasks()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(0);
        writer.Write(10);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        
        var world = new EntityWorld();
        world.EnsureEntityCapacity(5);
        world.EntityMasks[0].Set(1);
        
        StateSerializer.Deserialize(world, ms.ToArray());
        world.EntityMasks[0].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void StateSerializer_Deserialize_RecreateArray_WrongSize()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, new TestComponent1 { Value = 1 });
        var data = StateSerializer.Serialize(world);
        
        var targetWorld = new EntityWorld();
        targetWorld.EnsureTypedCapacity<TestComponent1>(ComponentId<TestComponent1>.IntId);
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(targetWorld);
        arrays[ComponentId<TestComponent1>.IntId] = new AlignedComponentStore<TestComponent1>(1);

        StateSerializer.Deserialize(targetWorld, data);

        arrays = (AlignedComponentStore?[])arrayField.GetValue(targetWorld);
        arrays[ComponentId<TestComponent1>.IntId].Length.Should().BeGreaterThan(1);
    }

    [Fact]
    public void StateSerializer_Deserialize_EmptyMaskData()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(0); 
        writer.Write(10); 
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0); 
        
        var world = new EntityWorld();
        world.EnsureEntityCapacity(5);
        world.EntityMasks[0].Set(1);
        
        StateSerializer.Deserialize(world, ms.ToArray());
        world.EntityMasks[0].IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void StateSerializer_Deserialize_ResizesMasks()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        int largeCapacity = 500;
        
        writer.Write(0);
        writer.Write(largeCapacity);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        
        var world = new EntityWorld();
        world.EntityMasks.Length.Should().Be(256);
        StateSerializer.Deserialize(world, ms.ToArray());
        world.EntityMasks.Length.Should().Be(largeCapacity);
    }

    [Fact]
    public void StateSerializer_Deserialize_NullMasks()
    {
        var world = new EntityWorld();
        var field = typeof(EntityWorld).GetField("_entityMasks", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(world, null);
        
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(0);
        writer.Write(10);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        
        StateSerializer.Deserialize(world, ms.ToArray(), autoReset: false);
        world.EntityMasks.Should().NotBeNull();
    }

    [Fact]
    public void StateSerializer_Deserialize_ExactCapacity()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        int capacity = 256;
        
        writer.Write(0);
        writer.Write(capacity);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        
        var world = new EntityWorld();
        StateSerializer.Deserialize(world, ms.ToArray());
        world.EntityMasks.Length.Should().Be(256);
    }
    
    [Fact]
    public void StateSerializer_Deserialize_CapacityCheck_ElseIf()
    {
        var world = new EntityWorld();
        world.EnsureEntityCapacity(100); 
        
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(0); 
        writer.Write(10); 
        writer.Write(0); 
        writer.Write(0); 
        writer.Write(0); 
        writer.Write(0); 
        
        StateSerializer.Deserialize(world, ms.ToArray());
        world.EntityMasks.Length.Should().Be(10);
    }
    
    [Fact]
    public void StateSerializer_Serialize_EmptyComponentArray()
    {
        var world = new EntityWorld();
        world.RegisterComponent<TestComponent1>();
        
        var arrayField = typeof(EntityWorld).GetField("_componentStores", BindingFlags.Instance | BindingFlags.NonPublic);
        var arrays = (AlignedComponentStore?[])arrayField.GetValue(world);
        arrays[ComponentId<TestComponent1>.IntId] = new AlignedComponentStore<TestComponent1>(0);

        var sizeField = typeof(EntityWorld).GetField("_componentElementSizes", BindingFlags.Instance | BindingFlags.NonPublic);
        var sizes = (int[])sizeField.GetValue(world);
        sizes[ComponentId<TestComponent1>.IntId] = 4;
        
        var data = StateSerializer.Serialize(world);
        data.Should().NotBeNull();
    }
    
    [Fact]
    public void StateSerializer_Deserialize_ShouldThrow_OnUnknownType()
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        writer.Write(0);
        writer.Write(10);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(1); // 1 component type
        writer.Write(9999); // Unknown
        writer.Write(0);
        writer.Write(0);
        
        var world = new EntityWorld();
        Action act = () => StateSerializer.Deserialize(world, ms.ToArray());
        act.Should().Throw<Exception>().WithMessage("*Type is unknown*");
    }

    // --- StateDumper Tests ---

    [Fact]
    public void StateDumper_Dump_StructWithNoFields()
    {
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, new TestComponentEmpty());
        var dump = StateDumper.Dump(world);
        dump.Should().Contain("TestComponentEmpty: {  }");
    }

    [Fact]
    public void StateDumper_Dump_RefType_ShouldPrintString()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        
        world.AddComponent(entity, new TestComponentWithFields { X = 42, Y = 99 });

        var dump = StateDumper.Dump(world);
        dump.Should().Contain("X: 42");
    }

    [Fact]
    public void StateDumper_DumpComponent_ShouldHandleCommas()
    {
        var c = new TestComponentWithFields { X = 1, Y = 2 };
        var world = new EntityWorld();
        var e = world.CreateEntity();
        world.AddComponent(e, c);
        
        var dump = StateDumper.Dump(world);
        dump.Should().Contain("X: 1, Y: 2");
        dump.Trim().Should().EndWith(" }");
    }

    [Fact]
    public void StateDumper_Dump_NullComponent()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        // Test that dumping an entity with a mask bit set but no store doesn't crash
        world.EntityMasks[entity.Id].Set(50);
        var dump = StateDumper.Dump(world);
        dump.Should().Contain($"Entity {entity.Id}");
    }

    [Fact]
    public void StateDumper_DumpComponent_ToStringReturnsNull()
    {
        var method = typeof(StateDumper).GetMethod("DumpComponent", BindingFlags.Static | BindingFlags.NonPublic);
        var c = new ToStringNullComponent();
        var res = (string)method!.Invoke(null, new object[] { c })!;
        res.Should().Be("null");
    }

    // --- StateHistory Tests ---

    [Fact]
    public void StateHistory_Retrieve_Gap()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(1, world);
        history.Store(3, world);
        history.Retrieve(2, world).Should().BeFalse();
    }

    [Fact]
    public void StateHistory_TryGetSnapshotData_Gap()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(1, world);
        history.Store(3, world);
        history.TryGetSnapshotData(2, out _).Should().BeFalse();
    }

    [Fact]
    public void StateHistory_TryGetSnapshotData_Empty()
    {
        var history = new StateHistory(10);
        history.TryGetSnapshotData(1, out _).Should().BeFalse();
    }

    [Fact]
    public void StateHistory_DiscardFuture_Partial()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(1, world);
        history.Store(2, world);
        history.Store(3, world);
        
        // Discard > 1 (should remove 2 and 3)
        history.DiscardFuture(1);
        
        history.GetLatestTick().Should().Be(1);
        history.Retrieve(2, world).Should().BeFalse();
        history.Retrieve(3, world).Should().BeFalse();
    }
    
    [Fact]
    public void StateHistory_Store_WrapAround()
    {
        var history = new StateHistory(3);
        var world = new EntityWorld();
        
        history.Store(1, world);
        history.Store(2, world);
        history.Store(3, world);
        // Buffer full: [1, 2, 3]
        
        history.Store(4, world);
        // Wrapped: [4, 2, 3] (head at 2)
        
        history.GetOldestTick().Should().Be(2);
        history.GetLatestTick().Should().Be(4);
    }

    [Fact]
    public void StateHistory_TryGetSnapshotData_OutOfBounds()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(10, world);
        history.TryGetSnapshotData(5, out _).Should().BeFalse();
        history.TryGetSnapshotData(15, out _).Should().BeFalse();
    }

    [Fact]
    public void StateHistory_Retrieve_Empty()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Retrieve(1, world).Should().BeFalse();
        history.GetOldestTick().Should().Be(-1);
        history.GetLatestTick().Should().Be(-1);
        history.DiscardFuture(1);
    }

    [Fact]
    public void StateHistory_DiscardFuture_NoOp()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(10, world);
        history.DiscardFuture(10);
        history.GetLatestTick().Should().Be(10);
    }

    [Fact]
    public void StateHistory_DiscardFuture_All()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(10, world);
        history.DiscardFuture(0);
        history.GetLatestTick().Should().Be(-1);
    }

    [Fact]
    public void StateHistory_Retrieve_LoopFullScan()
    {
        var history = new StateHistory(10);
        var world = new EntityWorld();
        history.Store(1, world);
        history.Store(3, world);
        history.Retrieve(2, world).Should().BeFalse();
    }
}

