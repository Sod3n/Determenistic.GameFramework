using System;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class AllocationTests
{
    private Dispatcher _dispatcher;
    private EntityWorld _world;
    private ActionScheduler _scheduler;
    private DenseComponentId _actionId;
    private TestAction _action;
    private Entity[] _entities;
    private const int EntityCount = 100;

    public AllocationTests()
    {
        _world = new EntityWorld();
        _dispatcher = new Dispatcher();
        _scheduler = new ActionScheduler();

        var actionService = new TestActionService();
        var reaction = new PreReactionService();

        _dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { reaction }
        );
        _dispatcher.EnableAction(actionService);
        _dispatcher.EnableReaction(reaction);
        _dispatcher.ActionDispatcher = new MockActionDispatcher();

        _actionId = (DenseComponentId)_dispatcher.GetDenseId<TestAction>();
        _action = new TestAction { Amount = 1 };
        
        _entities = new Entity[EntityCount];
        for (int i = 0; i < EntityCount; i++)
        {
            _entities[i] = _world.CreateEntity();
            _world.AddComponent(_entities[i], new TestComponent { Value = 0 });
        }
    }

    [Fact]
    public void Schedule_ShouldHaveZeroAllocation()
    {
        // Warmup
        RunSchedule();
        RunSchedule();
        
        long start = GC.GetAllocatedBytesForCurrentThread();
        RunSchedule();
        long end = GC.GetAllocatedBytesForCurrentThread();
        
        // Reset
        _scheduler.PruneHistory(11);
        
#if !DEBUG
        (end - start).Should().Be(0, $"Schedule: expected 0 bytes allocated, but got {end - start}");
#endif
    }

    [Fact]
    public void Execute_ShouldHaveZeroAllocation()
    {
        // Pre-schedule
        RunSchedule();
        
        // Warmup
        RunExecute();
        
        // Reset and Re-schedule for measurement
        _scheduler.PruneHistory(11);
        _world.ClearDirty();
        RunSchedule();

        long start = GC.GetAllocatedBytesForCurrentThread();
        RunExecute();
        long end = GC.GetAllocatedBytesForCurrentThread();
        
#if !DEBUG
        (end - start).Should().Be(0, $"Execute: expected 0 bytes allocated, but got {end - start}");
#endif
    }

    [Fact]
    public void Update_ShouldHaveZeroAllocation()
    {
        // Pre-schedule and Execute
        RunSchedule();
        RunExecute();
        
        // Warmup
        RunUpdate();
        
        // Reset? Update might consume components.
        // In our case, ActionService consumes the action component (removes it).
        // So we need to reset state to have components to process.
        
        // Clean slate
        _world.ResetComponents(false); // Soft reset
        _scheduler.PruneHistory(11);
        _world.ClearDirty();
        
        // Setup state for Update measurement
        RunSchedule();
        RunExecute(); // Now entities have actions
        
        long start = GC.GetAllocatedBytesForCurrentThread();
        RunUpdate();
        long end = GC.GetAllocatedBytesForCurrentThread();
        
#if !DEBUG
        (end - start).Should().Be(0, $"Update: expected 0 bytes allocated, but got {end - start}");
#endif
    }

    private void RunSchedule()
    {
        for (int i = 0; i < EntityCount; i++)
        {
            _scheduler.Schedule(_action, _actionId, _entities[i], 10);
        }
    }

    private void RunExecute()
    {
        _scheduler.ExecuteActions(10, _world, _dispatcher);
    }

    private void RunUpdate()
    {
        _dispatcher.Update(_world);
    }
}
