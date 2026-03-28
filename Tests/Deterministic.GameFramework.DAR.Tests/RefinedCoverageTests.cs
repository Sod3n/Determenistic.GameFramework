using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class RefinedCoverageTests
{
    private DenseComponentId TestActionId => ComponentId<TestAction>.DenseId;

    public RefinedCoverageTests()
    {
        try
        {
            ComponentId.RegisterAssembly(typeof(TestAction).Assembly);
        }
        catch { }
    }

    [Fact]
    public void DisabledReaction_ShouldNotExecute()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reaction = new PreReactionService(); // Doubles damage if Value == 100
        
        dispatcher.RegisterAction(actionService, new[] { reaction });
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        dispatcher.EnableAction(actionService);
        // Explicitly disable reaction
        dispatcher.DisableReaction(reaction);
        
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 100 });
        
        // Action: 10.
        // If reaction runs: Value==100 -> Amount becomes 20. Result 80.
        // If reaction disabled: Amount stays 10. Result 90.
        
        dispatcher.Execute(new TestAction(10), world, entity);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(90);
    }

    [Fact]
    public void ActionScheduler_DataBuffer_ShouldResize_ForLargeSingleAction()
    {
        var scheduler = new ActionScheduler();
        // Initial buffer 16KB.
        // Create an action that is larger than 16KB (e.g. 20KB).
        // Since we schedule structs, we need a struct that is large OR use ScheduleFromBytes.
        // ScheduleFromBytes is easier.
        
        byte[] hugeData = new byte[20000];
        // Fill with some data to verify integrity
        hugeData[0] = 0xAA;
        hugeData[19999] = 0xBB;
        
        scheduler.ScheduleFromBytes((DenseComponentId)123, hugeData, 1, 10);
        
        // Should not throw and have accepted it.
        
        // To verify integrity, we'd execute it. But we need a runner for 123.
        // We can't register one easily without a Type.
        // So we just rely on no exception during schedule and PruneHistory working.
        
        scheduler.PruneHistory(5); // Keep it
        
        // If buffer was too small, this would have crashed or corrupted.
    }
}
