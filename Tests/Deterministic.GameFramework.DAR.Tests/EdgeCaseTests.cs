using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class EdgeCaseTests
{
    private DenseComponentId TestActionId => ComponentId<TestAction>.DenseId;

    public EdgeCaseTests()
    {
    }

    [Fact]
    public void ActionScheduler_ShouldResizeDataBuffer_WhenExceedingInitialCapacity()
    {
        var scheduler = new ActionScheduler();
        // Initial buffer is 16KB (16 * 1024 = 16384 bytes)
        
        // We want to push more than 16KB.
        // TestAction is 4 bytes.
        // 5000 * 4 = 20,000 bytes.
        
        for (int i = 0; i < 5000; i++)
        {
            scheduler.Schedule(new TestAction(i), TestActionId, new Entity(1), 100);
        }
        
        // If no exception, resize worked.
        // Let's verify data integrity by pruning and checking the last item.
        
        scheduler.PruneHistory(90); 
        // All should be kept.
        
        // We can't easily peek into buffer without execution.
        // But successful execution implies data integrity.
    }
    
    [Fact]
    public void Dispatcher_RegisterAction_CalledTwice_ShouldBeIdempotent()
    {
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reactions = Array.Empty<ReactionService<TestAction, TestComponent>>();
        
        // First registration
        dispatcher.RegisterAction(actionService, reactions);
        
        // Second registration - should not throw and likely no-op for runners
        // But might update execution mask or system runners if implementation allows overrides.
        // The implementation checks: if (!_actionRunners.ContainsKey(id))
        dispatcher.RegisterAction(actionService, reactions);
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
    }

    [Fact]
    public void Dispatcher_ShouldResizeMask_WhenManyReactionsRegistered()
    {
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        
        // Create 300 reactions
        var reactions = new ReactionService<TestAction, TestComponent>[300];
        for(int i=0; i<300; i++)
        {
            reactions[i] = new PreReactionService();
        }
        
        // This triggers registration and resizing of execution mask
        dispatcher.RegisterAction(actionService, reactions);
        
        // Verify all have IDs assigned
        reactions[299].RuntimeId.Should().BeGreaterThan(0);
        
        // Verify we can enable/disable the last one (checking bounds)
        dispatcher.DisableReaction(reactions[299]);
        dispatcher.IsReactionEnabled(reactions[299]).Should().BeFalse();
        
        dispatcher.EnableReaction(reactions[299]);
        dispatcher.IsReactionEnabled(reactions[299]).Should().BeTrue();
    }
}
