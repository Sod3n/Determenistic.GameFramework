namespace Deterministic.GameFramework.Network.NetworkState;

public abstract class ReactionService<TAction, TTarget> where TAction : struct, IAction where TTarget : struct, IComponent
{
    public const int PriorityDefault = 0;
    public const int PriorityAbort = 1;
    public const int PriorityPrepare = 2;
    
    public abstract int Priority { get; }
    public abstract bool AfterActionExecuted { get; }
    
    protected abstract IsAborted React(TAction args, ref TTarget target, Context ctx);
    
    public struct IsAborted
    {
        public bool Value { get; set; }
    }
}