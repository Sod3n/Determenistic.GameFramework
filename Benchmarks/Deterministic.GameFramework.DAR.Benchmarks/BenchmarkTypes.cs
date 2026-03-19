using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Benchmarks.DAR;

[StableId("00000000-0000-0000-0000-000000000001")]
public struct BenchComponent : IComponent
{
    public int Value;
}

[StableId("00000000-0000-0000-0000-000000000002")]
public struct BenchAction : IAction
{
    public int Amount;
}

public class BenchActionService : ActionService<BenchAction, BenchComponent>
{
    protected override void ExecuteProcess(BenchAction args, ref BenchComponent target, Context ctx)
    {
        target.Value += args.Amount;
    }
}

public class BenchReactionService : ReactionService<BenchAction, BenchComponent>
{
    public override int Priority => 0;
    public override bool AfterActionExecuted => false;

    protected override IsAborted React(ref BenchAction args, ref BenchComponent target, Context ctx)
    {
        return new IsAborted { Value = false };
    }
}

public class MockActionDispatcher : IActionDispatcher
{
    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
    }
}
