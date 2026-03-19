using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR.Tests;

[StableId("00000000-0000-0000-0000-000000000001")]
public struct TestComponent : IComponent
{
    public int Value;
}

[StableId("00000000-0000-0000-0000-000000000002")]
public struct TestAction : IAction
{
    public int Amount;
    
    public TestAction(int amount)
    {
        Amount = amount;
    }
}

[StableId("00000000-0000-0000-0000-000000000003")]
public struct TestAction2 : IAction
{
    public int Amount;
}

public class TestActionService : ActionService<TestAction, TestComponent>
{
    protected override void ExecuteProcess(TestAction args, ref TestComponent target, Context ctx)
    {
        target.Value -= args.Amount;
    }
}

public class TestActionService2 : ActionService<TestAction2, TestComponent>
{
    protected override void ExecuteProcess(TestAction2 args, ref TestComponent target, Context ctx)
    {
        target.Value += args.Amount;
    }
}

public class PreReactionService : ReactionService<TestAction, TestComponent>
{
    public override int Priority => PriorityPrepare;
    public override bool AfterActionExecuted => false;

    protected override IsAborted React(ref TestAction args, ref TestComponent target, Context ctx)
    {
        // Example: Boost damage if value is full (just logic)
        if (target.Value == 100)
        {
            args.Amount *= 2;
        }
        return new IsAborted { Value = false };
    }
}

public class PostReactionService : ReactionService<TestAction, TestComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true;

    protected override IsAborted React(ref TestAction args, ref TestComponent target, Context ctx)
    {
        // Example: Clamp value
        if (target.Value < 0)
        {
            target.Value = 0;
        }
        return new IsAborted { Value = false };
    }
}

public class AbortReactionService : ReactionService<TestAction, TestComponent>
{
    public override int Priority => PriorityAbort;
    public override bool AfterActionExecuted => false;

    protected override IsAborted React(ref TestAction args, ref TestComponent target, Context ctx)
    {
        if (args.Amount > 9000)
        {
            return new IsAborted { Value = true };
        }
        return new IsAborted { Value = false };
    }
}

public class MockActionDispatcher : IActionDispatcher
{
    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        // No-op
    }
}
