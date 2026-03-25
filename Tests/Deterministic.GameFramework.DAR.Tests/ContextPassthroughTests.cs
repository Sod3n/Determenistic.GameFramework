using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class ContextPassthroughTests
{
    public ContextPassthroughTests()
    {
        try
        {
            ComponentId.RegisterAssembly(typeof(TestAction).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }
        catch { }
    }

    [Fact]
    public void Context_ShouldForwardAllEntityWorldMethods()
    {
        var world = new EntityWorld();
        var dispatcher = new MockActionDispatcher();
        var entity = world.CreateEntity(); // 0 (World), 1 (Entity)
        
        var context = new Context(world, entity, dispatcher);

        // 1. CreateEntity
        var e2 = context.CreateEntity();
        e2.Id.Should().BeGreaterThan(0);
        
        var e3 = context.CreateEntity<TestComponent>();
        world.HasComponent<TestComponent>(e3).Should().BeTrue();

        // 2. Add/Remove/Get/Has
        context.AddComponent(e2, new TestComponent { Value = 10 });
        context.HasComponent<TestComponent>(e2).Should().BeTrue();
        context.GetComponent<TestComponent>(e2).Value.Should().Be(10);
        
        context.TryGetComponent<TestComponent>(e2).Should().NotBeNull();
        context.TryGetComponent<TestComponent>(entity).Should().BeNull();
        
        context.RemoveComponent<TestComponent>(e2);
        context.HasComponent<TestComponent>(e2).Should().BeFalse();
        
        // 3. Register/RawArray
        context.RegisterComponent<TestComponent>();
        var store = context.GetComponentStore<TestComponent>();
        store.Should().NotBeNull();
        
        // 4. Filters
        // Create matching entities
        var e4 = context.CreateEntity();
        context.AddComponent(e4, new TestComponent());
        
        context.Filter<TestComponent>().Should().Contain(e => e.Id == e4.Id);
        
        // Filter<T1, T2>
        context.AddComponent(e4, new TestAction()); // Using TestAction as component
        context.Filter<TestComponent, TestAction>().Should().Contain(e => e.Id == e4.Id);
        
        // Filter<T1, T2, T3> - Need 3rd component. World is one.
        context.AddComponent(e4, new World());
        context.Filter<TestComponent, TestAction, World>().Should().Contain(e => e.Id == e4.Id);
        
        // 5. ForEach
        int count1 = 0;
        context.ForEach<TestComponent>((ref TestComponent c) => count1++);
        count1.Should().BeGreaterThan(0);
        
        int count2 = 0;
        context.ForEach<TestComponent, TestAction>((ref TestComponent c, ref TestAction a) => count2++);
        count2.Should().BeGreaterThan(0);
        
        int count3 = 0;
        context.ForEach<TestComponent>((Entity e, ref TestComponent c) => count3++);
        count3.Should().BeGreaterThan(0);
        
        int count4 = 0;
        context.ForEach<TestComponent, TestAction>((Entity e, ref TestComponent c, ref TestAction a) => count4++);
        count4.Should().BeGreaterThan(0);
        
        // 6. Delete
        context.DeleteEntity(e4);
        context.HasComponent<TestComponent>(e4).Should().BeFalse();
        
        // 7. Dirty
        context.ClearDirty();
        context.GetDirtyEntities().Should().BeEmpty();
        context.CreateEntity(); // Should mark dirty internally
        // Actually CreateEntity marks dirty? EntityWorld.EnsureEntityCapacity doesn't.
        // AddComponent does.
        var e5 = context.CreateEntity();
        context.AddComponent(e5, new TestComponent());
        context.GetDirtyEntities().Should().Contain(e5.Id);
        
        // 8. Explicit Interface Implementation for AddComponent returning ref
        ((IEntityWorld)context).AddComponent(e5, new TestComponent { Value = 99 });
        context.GetComponent<TestComponent>(e5).Value.Should().Be(99);
    }
}
