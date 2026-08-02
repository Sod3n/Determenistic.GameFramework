using System;
using System.Reflection;
using Deterministic.GameFramework.Serialization;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class DeterministicHashingTests : IDisposable
{
    public DeterministicHashingTests()
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
    public void StateHash_ShouldBeIdentical_WhenCapacitiesDiffer()
    {
        // 1. Create a "Server" world with default capacity (256)
        var serverWorld = new EntityWorld();
        var e1 = serverWorld.CreateEntity();
        serverWorld.AddComponent(e1, new TestComponent1 { Value = 42 });

        // 2. Create a "Client" world and force it to expand its capacity
        var clientWorld = new EntityWorld();
        // Force expansion beyond 256
        for (int i = 0; i < 300; i++)
        {
            clientWorld.CreateEntity();
        }

        // 3. Serialize Server state and Deserialize into Client
        // This simulates a full state sync from Server to Client
        byte[] serverData = StateSerializer.Serialize(serverWorld);
        StateSerializer.Deserialize(clientWorld, serverData, syncComponentIds: true, autoReset: true);

        // 4. Logically, they should now be identical
        clientWorld.NextEntityId.Should().Be(serverWorld.NextEntityId);

        // Verify that capacities are normalized to NextEntityId after the fix
        clientWorld._entityMasks.Length.Should().Be(serverWorld.NextEntityId);

        // 5. Calculate hashes
        var serverHash = StateHasher.Hash(serverWorld);
        var clientHash = StateHasher.Hash(clientWorld);

        // This should now pass
        clientHash.Should().Be(serverHash, "Hashes must match after a full state sync, regardless of previous capacity");
    }

    [Fact]
    public void StateHash_ShouldBeIdentical_WhenComponentCapacitiesDiffer()
    {
        // 1. Create Server world
        var serverWorld = new EntityWorld();
        var e1 = serverWorld.CreateEntity();
        serverWorld.AddComponent(e1, new TestComponent1 { Value = 10 });

        // 2. Create Client world and expand component array for TestComponent1
        var clientWorld = new EntityWorld();
        var e2 = clientWorld.CreateEntity();
        clientWorld.AddComponent(e2, new TestComponent1 { Value = 20 });
        // Force expansion of TestComponent1 array (default is 32)
        for (int i = 0; i < 100; i++)
        {
            var tmp = clientWorld.CreateEntity();
            clientWorld.AddComponent(tmp, new TestComponent1 { Value = i });
        }

        // 3. Sync Server -> Client
        byte[] serverData = StateSerializer.Serialize(serverWorld);
        StateSerializer.Deserialize(clientWorld, serverData, syncComponentIds: true, autoReset: true);

        // 4. Verify hashes
        var clientCompArray = clientWorld._componentStores[ComponentId<TestComponent1>.IntId]!;
        // Component arrays are also normalized to NextEntityId
        clientCompArray.Length.Should().Be(serverWorld.NextEntityId);

        var serverHash = StateHasher.Hash(serverWorld);
        var clientHash = StateHasher.Hash(clientWorld);

        clientHash.Should().Be(serverHash, "Hashes must match even if component arrays had different capacities previously");
    }

    [Fact]
    public void GetComponent_ShouldNotSetMaskBit()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity(); // Entity 1 (0 is World)

        // Verify component is not present
        world.HasComponent<TestComponent1>(entity).Should().BeFalse();

        // Call GetComponent (the side-effect prone method)
        world.GetComponent<TestComponent1>(entity);

        // Verify component is STILL not present in the mask
        world.HasComponent<TestComponent1>(entity).Should().BeFalse("GetComponent should not have the side effect of adding the component to the mask");

        // Add it properly
        world.AddComponent(entity, new TestComponent1 { Value = 10 });
        world.HasComponent<TestComponent1>(entity).Should().BeTrue();
    }
}
