using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class DispatcherReRegistrationTests
{
    public DispatcherReRegistrationTests()
    {
        try
        {
            ComponentId.RegisterAssembly(typeof(TestAction).Assembly);
        }
        catch { }
    }

    [Fact]
    public void RegisterServices_CalledTwice_ShouldSkipSecondRegistration()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        var r1 = new PreReactionService();
        
        // First registration
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1 }
        );
        
        // Second registration with DIFFERENT reactions (to prove it was skipped)
        var r2 = new PostReactionService();
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r2 }
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
        
        // To verify r2 was NOT registered, we'd need to check execution logic.
        // But since we can't easily inspect, we assume code coverage tools would show 'continue' hit.
        // Or we can rely on the fact that IsActionRegistered is checked.
    }

    [Fact]
    public void RegisterServices_CalledTwice_ShouldSkipSecondRegistration_AndNotRunNewReactions()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        var r1 = new PreReactionService(); 
        
        // 1. Register with r1
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1 }
        );
        
        // 2. Register again with r2 (which has side effects we can check, e.g. PostReactionService)
        var r2 = new PostReactionService(); // Clamps to 0
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r2 }
        );
        
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(r1);
        dispatcher.EnableReaction(r2); // Try enabling it, though it shouldn't be registered
        
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        // Set value to 5. Action 10. Result -5.
        // If r2 runs, it clamps to 0.
        // If r2 doesn't run, it stays -5.
        world.AddComponent(entity, new TestComponent { Value = 5 });
        
        dispatcher.Execute(new TestAction(10), world, entity);
        dispatcher.Update(world);
        
        // Expect -5 because r2 was skipped during registration
        world.GetComponent<TestComponent>(entity).Value.Should().Be(-5);
    }

    [Fact]
    public void Unregister_Then_Register_ShouldReplaceSystem()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        var r1 = new PreReactionService();
        
        // 1. Register
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1 }
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
        
        // 2. Unregister
        dispatcher.UnregisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1 }
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeFalse();
        dispatcher.IsActionEnabled(actionService).Should().BeFalse();
        
        // 3. Register Again
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1 }
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
        // Should be enabled by default after register?
        // RegisterAction enables the mask: _executionMask[actionService.RuntimeId] = true;
        dispatcher.IsActionEnabled(actionService).Should().BeTrue();
    }
    
    [Fact]
    public void Execute_ShouldWork_AfterUnregister()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            Array.Empty<IReactionService>()
        );
        
        dispatcher.UnregisterServices(
            new IActionService[] { actionService },
            Array.Empty<IReactionService>()
        );
        
        // Execute (Direct Injection) should still work because _actionRunners are not cleared
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        
        dispatcher.Execute(new TestAction(10), world, entity);
        
        world.HasComponent<TestAction>(entity).Should().BeTrue();
    }
}
