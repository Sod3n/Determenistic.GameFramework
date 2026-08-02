using Deterministic.GameFramework.ECS;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.DAR.Tests;

public class CoverageTests
{
    public CoverageTests()
    {
    }

    [Fact]
    public void Context_PassThrough_ShouldWork()
    {
        var world = new EntityWorld();
        var dispatcher = new MockActionDispatcher();
        var entity = world.CreateEntity();
        
        var context = new Context(world, entity, dispatcher);

        // Verify IEntityWorld implementation forwards to World
        context.NextEntityId.Should().Be(world.NextEntityId);
        context.EntityMasks.Should().BeSameAs(world.EntityMasks);
        
        var e2 = context.CreateEntity();
        world.HasComponent<World>(e2).Should().BeFalse(); // CreateEntity without T
        
        var e3 = context.CreateEntity<TestComponent>();
        world.HasComponent<TestComponent>(e3).Should().BeTrue();
        
        context.AddComponent(e2, new TestComponent { Value = 123 });
        context.GetComponent<TestComponent>(e2).Value.Should().Be(123);
        
        context.RemoveComponent<TestComponent>(e2);
        context.HasComponent<TestComponent>(e2).Should().BeFalse();
        
        context.DeleteEntity(e2);
        // Checking deletion via mask or just trusting it forwarded
        
        // ExternalState
        context.ExternalState.Should().BeSameAs(world.ExternalState);
        context.ExternalState = new Dictionary<string, byte[]>();
        world.ExternalState.Should().BeSameAs(context.ExternalState);
        
        // Dispatch
        // Should not throw
        context.Dispatch(new TestAction(1), entity);
    }
    
    [Fact]
    public void Dispatcher_GetDenseId_ShouldThrow_ForUnregisteredAction()
    {
        var dispatcher = new ReactionDispatcher();
        // TestAction is registered in ComponentId, but not in Dispatcher
        
        Action act = () => dispatcher.GetDenseId<TestAction>();
        
        act.Should().Throw<Exception>()
           .WithMessage("*not registered in Dispatcher*");
    }

    [Fact]
    public void ReactionService_ShouldReact_False_ShouldSkipReact()
    {
        var world = new EntityWorld();
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        var reaction = new ConditionalReactionService(); // Returns ShouldReact = false for Amount 0
        
        dispatcher.RegisterActionWithReactions(actionService, new[] { reaction });
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(reaction);
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        
        var entity = world.CreateEntity();
        world.AddComponent(entity, new TestComponent { Value = 100 });
        
        // 1. Action with Amount 0 -> ShouldReact returns false -> React not called -> Value remains 100 (minus action effect 0)
        // Wait, action logic executes: target.Value -= args.Amount (0) -> Value = 100
        // Reaction logic (if called): target.Value = 999
        
        dispatcher.Execute(new TestAction(0), world, entity);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(100);
        
        // 2. Action with Amount 1 -> ShouldReact returns true -> React called -> Value = 999
        dispatcher.Execute(new TestAction(1), world, entity);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(999);
    }

    [Fact]
    public void RegisterServices_ShouldGroupReactionsCorrectly()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        var r1 = new PreReactionService();
        var r2 = new PostReactionService();
        
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { r1, r2 }
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
        // We can't easily inspect the internal reaction list of the dispatcher without reflection or execution side effects.
        // Let's rely on execution to verify both reactions run.
        
        dispatcher.ActionDispatcher = new MockActionDispatcher();
        dispatcher.EnableAction(actionService);
        dispatcher.EnableReaction(r1);
        dispatcher.EnableReaction(r2);
        
        var world = new EntityWorld();
        var entity = world.CreateEntity();
        // Initial 100
        world.AddComponent(entity, new TestComponent { Value = 100 });
        
        // Action: 10
        // PreReaction: Value==100 -> Amount * 2 = 20
        // Action: Value - 20 = 80
        // PostReaction: Clamp(80) -> 80 (No-op as > 0)
        
        // Let's change PostReaction to something observable or use current one.
        // PostReaction clamps < 0 to 0.
        // Let's make result negative to test PostReaction.
        
        // Reset entity
        world.GetComponent<TestComponent>(entity).Value = 10;
        // Action: 20
        // PreReaction: Value!=100 -> Amount 20
        // Action: 10 - 20 = -10
        // PostReaction: Clamp(-10) -> 0
        
        dispatcher.Execute(new TestAction(20), world, entity);
        dispatcher.Update(world);
        
        world.GetComponent<TestComponent>(entity).Value.Should().Be(0);
    }

    [Fact]
    public void RegisterServices_ShouldHandleActionWithNoReactions()
    {
        var dispatcher = new ReactionDispatcher();
        var actionService = new TestActionService();
        
        dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { } // Empty reactions
        );
        
        dispatcher.IsActionRegistered(typeof(TestAction)).Should().BeTrue();
    }
}

public class ConditionalReactionService : ReactionService<TestAction, TestComponent>
{
    public override int Priority => 0;
    public override bool AfterActionExecuted => true;

    protected override bool ShouldReact(TestAction args, TestComponent target, Context ctx)
    {
        return args.Amount != 0;
    }

    protected override IsAborted React(ref TestAction args, ref TestComponent target, Context ctx)
    {
        target.Value = 999;
        return new IsAborted { Value = false };
    }
}
