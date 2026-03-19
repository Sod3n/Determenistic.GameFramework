using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class ActionSchedulerTests
{
    public ActionSchedulerTests()
    {
    }

    private DenseComponentId TestActionId => ComponentId<TestAction>.DenseId;

    [Fact]
    public void EarliestDirtyTick_ShouldTrackMinimumScheduledTick()
    {
        var scheduler = new ActionScheduler();
        
        scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
        
        var action = new TestAction(10);
        scheduler.Schedule(action, TestActionId, new Entity(1), 5);
        scheduler.EarliestDirtyTick.Should().Be(5);
        
        scheduler.Schedule(action, TestActionId, new Entity(2), 3);
        scheduler.EarliestDirtyTick.Should().Be(3);
        
        scheduler.Schedule(action, TestActionId, new Entity(3), 10);
        scheduler.EarliestDirtyTick.Should().Be(3);
    }

    [Fact]
    public void ScheduleFromBytes_ShouldUpdateDirtyTick()
    {
        var scheduler = new ActionScheduler();
        
        var action = new TestAction(15);
        int size = Marshal.SizeOf<TestAction>();
        byte[] bytes = new byte[size];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(bytes, in action);
#else
        MemoryMarshal.Write(bytes, ref action);
#endif
        
        scheduler.ScheduleFromBytes(TestActionId, bytes, 1, 7);
        scheduler.EarliestDirtyTick.Should().Be(7);
    }

    [Fact]
    public void ExecuteActions_ShouldResetDirtyTickAfterExecution()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        dispatcher.RegisterAction(actionService, Array.Empty<ReactionService<TestAction, TestComponent>>());
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        var scheduler = new ActionScheduler();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 100 });
        
        var action = new TestAction(10);
        scheduler.Schedule(action, TestActionId, entity, 5);
        
        scheduler.EarliestDirtyTick.Should().Be(5);
        
        scheduler.ExecuteActions(5, world, dispatcher);
        
        scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
    }

    [Fact]
    public void ExecuteActions_ShouldExecuteInDeterministicOrder()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        dispatcher.RegisterAction(actionService, Array.Empty<ReactionService<TestAction, TestComponent>>());
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        // We need to enable the action service in the dispatcher
        dispatcher.EnableAction(actionService);
        // Dispatcher.Update iterates system runners.
        
        var scheduler = new ActionScheduler();
        
        var e1 = world.CreateEntity();
        var e2 = world.CreateEntity();
        var e3 = world.CreateEntity();
        
        world.AddComponent(e1, new TestComponent { Value = 100 });
        world.AddComponent(e2, new TestComponent { Value = 100 });
        world.AddComponent(e3, new TestComponent { Value = 100 });
        
        var denseId = dispatcher.GetDenseId<TestAction>();
        scheduler.Schedule(new TestAction(30), (DenseComponentId)denseId, e3, 10);
        scheduler.Schedule(new TestAction(10), (DenseComponentId)denseId, e1, 10);
        scheduler.Schedule(new TestAction(20), (DenseComponentId)denseId, e2, 10);
        
        // ExecuteActions puts components on entities
        scheduler.ExecuteActions(10, world, dispatcher);
        
        // Dispatcher.Update runs the systems which process the components
        dispatcher.Update(world);
        
        // Check results
        world.GetComponent<TestComponent>(e1).Value.Should().Be(90);
        world.GetComponent<TestComponent>(e2).Value.Should().Be(80);
        world.GetComponent<TestComponent>(e3).Value.Should().Be(70);
    }

    [Fact]
    public void PruneHistory_ShouldRemoveOldActions()
    {
        var scheduler = new ActionScheduler();
        
        var action = new TestAction(10);
        scheduler.Schedule(action, TestActionId, new Entity(1), 5);
        scheduler.Schedule(action, TestActionId, new Entity(2), 10);
        scheduler.Schedule(action, TestActionId, new Entity(3), 15);
        
        scheduler.PruneHistory(12);
        
        scheduler.EarliestDirtyTick.Should().Be(15);
    }

    [Fact]
    public void PruneHistory_ShouldResetDirtyTickWhenAllPruned()
    {
        var scheduler = new ActionScheduler();
        
        var action = new TestAction(10);
        scheduler.Schedule(action, TestActionId, new Entity(1), 5);
        scheduler.Schedule(action, TestActionId, new Entity(2), 10);
        
        scheduler.PruneHistory(20);
        
        scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
    }

    [Fact]
    public void OnActionScheduled_ShouldFireEvent()
    {
        var scheduler = new ActionScheduler();
        
        bool eventFired = false;
        int capturedStableId = 0;
        long capturedTick = 0;
        
        scheduler.OnActionScheduled += (StableId, data, targetId, tick) =>
        {
            eventFired = true;
            capturedStableId = StableId;
            capturedTick = tick;
        };
        
        var action = new TestAction(10);
        scheduler.Schedule(action, TestActionId, new Entity(5), 7);
        
        eventFired.Should().BeTrue();
        capturedStableId.Should().Be(TestActionId.Value);
        capturedTick.Should().Be(7);
    }

    [Fact]
    public void ExecuteActions_ShouldOnlyExecuteActionsForSpecificTick()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        dispatcher.RegisterAction(actionService, Array.Empty<ReactionService<TestAction, TestComponent>>());
        dispatcher.EnableAction(actionService);

        var scheduler = new ActionScheduler();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 100 });
        
        var denseId = dispatcher.GetDenseId<TestAction>();
        scheduler.Schedule(new TestAction(10), (DenseComponentId)denseId, entity, 5);
        scheduler.Schedule(new TestAction(20), (DenseComponentId)denseId, entity, 10);
        
        // Tick 5
        scheduler.ExecuteActions(5, world, dispatcher);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(90);
        
        // Tick 10
        scheduler.ExecuteActions(10, world, dispatcher);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(70);
    }

    [Fact]
    public void ScheduleFromBytes_ShouldReturnTooOld_WhenTickIsBelowMinAllowed()
    {
        var scheduler = new ActionScheduler();
        scheduler.PruneHistory(10); // Sets MinAllowedTick to 10

        var action = new TestAction(15);
        int size = Marshal.SizeOf<TestAction>();
        byte[] bytes = new byte[size];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(bytes, in action);
#else
        MemoryMarshal.Write(bytes, ref action);
#endif

        var result = scheduler.ScheduleFromBytes(TestActionId, bytes, 1, 5);
        
        result.Should().Be(ActionScheduler.ScheduleResult.TooOld);
        scheduler.EarliestDirtyTick.Should().Be(long.MaxValue); // Should not have dirtied anything
    }

    [Fact]
    public void ScheduleFromBytes_ShouldReturnDuplicate_WhenSameActionScheduled()
    {
        var scheduler = new ActionScheduler();
        
        var action = new TestAction(15);
        int size = Marshal.SizeOf<TestAction>();
        byte[] bytes = new byte[size];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(bytes, in action);
#else
        MemoryMarshal.Write(bytes, ref action);
#endif

        scheduler.ScheduleFromBytes(TestActionId, bytes, 1, 10);
        var result = scheduler.ScheduleFromBytes(TestActionId, bytes, 1, 10);
        
        result.Should().Be(ActionScheduler.ScheduleResult.Duplicate);
    }
}
