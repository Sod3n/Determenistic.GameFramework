namespace Deterministic.GameFramework.CoreV2;

public abstract class ActionService<TAction, TTarget> 
    where TAction : struct, IAction 
    where TTarget : struct, IComponent
{
    // Make execution available to the dispatcher
    internal void InternalExecute(TAction args, ref TTarget target, Context ctx)
    {
        ExecuteProcess(args, ref target, ctx);
    }

    protected abstract void ExecuteProcess(TAction args, ref TTarget target, Context ctx);
}
