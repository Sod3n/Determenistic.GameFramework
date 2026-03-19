using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR;

public interface IActionService
{
    Type ActionType { get; }
    Type TargetType { get; }
    
    int RuntimeId { get; set; }
}

public abstract class ActionService<TAction, TTarget> : IActionService
    where TAction : struct, IAction 
    where TTarget : struct, IComponent
{
    public Type ActionType => typeof(TAction);
    public Type TargetType => typeof(TTarget);
    public int RuntimeId { get; set; } = -1;

    // Make execution available to the dispatcher
    internal void InternalExecute(TAction args, ref TTarget target, Context ctx)
    {
        ExecuteProcess(args, ref target, ctx);
    }

    protected abstract void ExecuteProcess(TAction args, ref TTarget target, Context ctx);
}
