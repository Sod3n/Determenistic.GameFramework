using System.Reflection;
using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class GapFillingTests
{
    private DenseComponentId TestActionId => ComponentId<TestAction>.DenseId;

    public GapFillingTests()
    {
    }

    [Fact]
    public void AutoActionAttribute_ShouldBeRetrievable()
    {
        var type = typeof(TestTypeWithAttribute);
        var field = type.GetField("MyField");
        var attr = field.GetCustomAttribute<AutoActionAttribute>();
        attr.Should().NotBeNull();
    }

    private class TestTypeWithAttribute
    {
        [AutoAction]
        public int MyField = 0;
    }

    [Fact]
    public void Disposable_ShouldBeIdempotent()
    {
        var dispatcher = new ReactionDispatcher();
        var service = new TestActionService();
        dispatcher.RegisterAction(service);
        dispatcher.DisableAction(service);

        var disposable = dispatcher.EnableActions(new[] { service });
        
        dispatcher.IsActionEnabled(service).Should().BeTrue();
        
        disposable.Dispose();
        dispatcher.IsActionEnabled(service).Should().BeFalse();
        
        // Second dispose should not throw and not change state
        disposable.Dispose();
        dispatcher.IsActionEnabled(service).Should().BeFalse();
    }

    [Fact]
    public void EnableDisable_UnregisteredService_ShouldDoNothing()
    {
        var dispatcher = new ReactionDispatcher();
        var service = new TestActionService(); // Not registered, RuntimeId = -1
        
        // Should not throw
        dispatcher.EnableAction(service);
        dispatcher.DisableAction(service);
        
        dispatcher.IsActionEnabled(service).Should().BeFalse();
    }

    [Fact]
    public void EnableDisable_UnregisteredReaction_ShouldDoNothing()
    {
        var dispatcher = new ReactionDispatcher();
        var service = new PreReactionService(); // Not registered, RuntimeId = -1
        
        // Should not throw
        dispatcher.EnableReaction(service);
        dispatcher.DisableReaction(service);
        
        dispatcher.IsReactionEnabled(service).Should().BeFalse();
    }

    [Fact]
    public void ExecuteByteAction_InvalidId_ShouldDoNothing()
    {
        var dispatcher = new ReactionDispatcher();
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        
        // Random ID 9999
        dispatcher.ExecuteByteAction(9999, new byte[10], 0, world, entity);
        
        // No side effects expected
    }

    [Fact]
    public void RegisterServices_ShouldCatchAndLog_WhenRegistrationFails()
    {
        var dispatcher = new ReactionDispatcher();
        var badService = new BadActionService();
        
        // This should not throw, but catch exception internally and log to Console
        dispatcher.RegisterServices(
            new IActionService[] { badService },
            Array.Empty<IReactionService>()
        );
        
        // Verify it wasn't registered
        dispatcher.IsActionRegistered(typeof(BadAction)).Should().BeFalse();
    }

    // Invalid types that don't satisfy constraints
    public struct BadAction { } // Not IAction
    public struct BadComponent { } // Not IComponent
    
    public class BadActionService : IActionService
    {
        public Type ActionType => typeof(BadAction);
        public Type TargetType => typeof(BadComponent);
        public int RuntimeId { get; set; } = -1;
    }
}
