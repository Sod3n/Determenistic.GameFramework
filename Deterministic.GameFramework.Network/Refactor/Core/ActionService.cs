namespace Deterministic.GameFramework.Network.NetworkState;

public abstract class ActionService<TAction, TTarget> where TAction : struct, IAction where TTarget : struct, IComponent
{
    protected abstract void ExecuteProcess(TAction args, ref TTarget target, Context ctx);
}