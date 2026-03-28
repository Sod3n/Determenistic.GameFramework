using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class DispatcherLifecycleTests
{
    private readonly Dispatcher _dispatcher;
    private readonly TestActionService _actionService;
    private readonly PreReactionService _reactionService;

    public DispatcherLifecycleTests()
    {
        _dispatcher = new Dispatcher();
        _actionService = new TestActionService();
        _reactionService = new PreReactionService();
        
        _dispatcher.RegisterServices(
            new IActionService[] { _actionService },
            new IReactionService[] { _reactionService });
    }

    [Fact]
    public void EnableDisableAction_ShouldUpdateState()
    {
        _dispatcher.DisableAction(_actionService);
        _dispatcher.IsActionEnabled(_actionService).Should().BeFalse();

        _dispatcher.EnableAction(_actionService);
        _dispatcher.IsActionEnabled(_actionService).Should().BeTrue();
    }

    [Fact]
    public void EnableDisableReaction_ShouldUpdateState()
    {
        _dispatcher.DisableReaction(_reactionService);
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeFalse();

        _dispatcher.EnableReaction(_reactionService);
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeTrue();
    }

    [Fact]
    public void EnableActions_ShouldReturnDisposable_AndRevertOnDispose()
    {
        // Setup: Initially disabled
        _dispatcher.DisableAction(_actionService);
        _dispatcher.IsActionEnabled(_actionService).Should().BeFalse();

        // Act: Enable via scope
        using (var scope = _dispatcher.EnableActions(new[] { _actionService }))
        {
            _dispatcher.IsActionEnabled(_actionService).Should().BeTrue();
        }

        // Assert: Reverted to disabled
        _dispatcher.IsActionEnabled(_actionService).Should().BeFalse();
    }
    
    [Fact]
    public void EnableActions_ShouldNotRevert_IfAlreadyEnabled()
    {
        // Setup: Initially enabled
        _dispatcher.EnableAction(_actionService);
        _dispatcher.IsActionEnabled(_actionService).Should().BeTrue();

        // Act: Enable via scope (should detect it was already enabled and NOT add it to the disable list)
        using (var scope = _dispatcher.EnableActions(new[] { _actionService }))
        {
            _dispatcher.IsActionEnabled(_actionService).Should().BeTrue();
        }

        // Assert: Stays enabled because it was enabled before
        _dispatcher.IsActionEnabled(_actionService).Should().BeTrue();
    }

    [Fact]
    public void EnableReactions_ShouldReturnDisposable_AndRevertOnDispose()
    {
        // Setup: Initially disabled
        _dispatcher.DisableReaction(_reactionService);
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeFalse();

        // Act: Enable via scope
        using (var scope = _dispatcher.EnableReactions(new[] { _reactionService }))
        {
            _dispatcher.IsReactionEnabled(_reactionService).Should().BeTrue();
        }

        // Assert: Reverted to disabled
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeFalse();
    }

    [Fact]
    public void DisableActions_ShouldDisableMultiple()
    {
        _dispatcher.EnableAction(_actionService);
        
        _dispatcher.DisableActions(new[] { _actionService });
        
        _dispatcher.IsActionEnabled(_actionService).Should().BeFalse();
    }

    [Fact]
    public void DisableReactions_ShouldDisableMultiple()
    {
        _dispatcher.EnableReaction(_reactionService);
        
        _dispatcher.DisableReactions(new[] { _reactionService });
        
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeFalse();
    }

    [Fact]
    public void UnregisterServices_ShouldRemoveFromRegistry()
    {
        _dispatcher.UnregisterServices(
            new IActionService[] { _actionService },
            new IReactionService[] { _reactionService }
        );

        // System runner should be gone
        _dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeFalse();
        
        // Runtime flag should be disabled (implementation detail: Unregister calls Disable)
        _dispatcher.IsActionEnabled(_actionService).Should().BeFalse();
        _dispatcher.IsReactionEnabled(_reactionService).Should().BeFalse();
    }
    
    [Fact]
    public void GetActionType_ShouldReturnCorrectType()
    {
        var denseId = _dispatcher.GetDenseId<TestAction>();
        var type = _dispatcher.GetActionType(denseId);
        
        type.Should().Be(typeof(TestAction));
    }

    [Fact]
    public void GetActionType_ShouldReturnNull_ForUnknownId()
    {
        var type = _dispatcher.GetActionType(99999);
        type.Should().BeNull();
    }

    [Fact]
    public void Execute_ShouldRunDirectly()
    {
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 50 });

        _dispatcher.Execute(new TestAction(10), world, entity);

        // Direct Execute adds component to world, but does not run the system loop
        // It's equivalent to Schedule -> ExecuteActions (ECS injection)
        // Wait, looking at Dispatcher.Execute:
        // _actionRunners[id] = (Action<TAction, EntityWorld, Entity>)Runner;
        // Runner: state.AddComponent(entity, action);
        
        world.HasComponent<TestAction>(entity).Should().BeTrue();
        world.GetComponent<TestAction>(entity).Amount.Should().Be(10);
    }
}
