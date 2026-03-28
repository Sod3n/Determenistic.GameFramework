using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class DispatcherTests
{
    public DispatcherTests()
    {
        // Ensure types are registered
        try
        {
            ComponentId.RegisterAssembly(typeof(TestAction).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }
        catch
        {
            // Ignore if already registered
        }
    }

    [Fact]
    public void Dispatcher_ShouldRegisterServicesCorrectly()
    {
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reaction = new PreReactionService();
        
        dispatcher.RegisterServices(
            new IActionService[] { actionService }, 
            new IReactionService[] { reaction });
            
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
    }
    
    [Fact]
    public void PreReaction_ShouldModifyAction_BeforeExecution()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reaction = new PreReactionService();
        
        dispatcher.RegisterAction(actionService, new[] { reaction });
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(reaction);
        
        // Use manual ActionDispatcher injection if needed, or rely on normal flow
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 100 }); // Value 100 triggers double damage in PreReaction
        
        // Manually trigger the system (normally scheduler does this part via ExecuteActions adding the component)
        world.AddComponent(entity, new TestAction(10));
        
        dispatcher.Update(world);
        
        // Expected: 100 - (10 * 2) = 80
        world.GetComponent<TestComponent>(entity).Value.Should().Be(80);
    }
    
    [Fact]
    public void PostReaction_ShouldRun_AfterExecution()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reaction = new PostReactionService(); // Clamps to 0
        
        dispatcher.RegisterAction(actionService, new[] { reaction });
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(reaction);
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 5 });
        
        // Action: -10 -> Result -5. PostReaction Clamps to 0.
        world.AddComponent(entity, new TestAction(10));
        
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(0);
    }

    [Fact]
    public void AbortReaction_ShouldStopExecution()
    {
        var world = new EntityWorld();
        var dispatcher = new Dispatcher();
        var actionService = new TestActionService();
        var reaction = new AbortReactionService(); // Aborts if amount > 9000
        
        dispatcher.RegisterAction(actionService, new[] { reaction });
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(reaction);
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 10000 });
        
        // Action > 9000 -> Should abort
        world.AddComponent(entity, new TestAction(9001));
        
        dispatcher.Update(world);
        
        // Value should be unchanged
        world.GetComponent<TestComponent>(entity).Value.Should().Be(10000);
    }
}
