namespace Deterministic.GameFramework.CoreV2;

public interface IReactionService
{
    Type ActionType { get; }
    Type TargetType { get; }
    
    int RuntimeId { get; set; }
}

public abstract class ReactionService<TAction, TTarget> : IReactionService
    where TAction : struct, IAction 
    where TTarget : struct, IComponent
{
    public Type ActionType => typeof(TAction);
    public Type TargetType => typeof(TTarget);
    
    public int RuntimeId { get; set; } = -1;

    public const int PriorityDefault = 0;
    public const int PriorityAbort = 1;
    public const int PriorityPrepare = 2;
    
    public abstract int Priority { get; }
    
    // If true, runs after the ActionService has processed
    public abstract bool AfterActionExecuted { get; }
    
    internal IsAborted InternalReact(ref TAction args, ref TTarget target, Context ctx)
    {
        if (!ShouldReact(args, target, ctx))
        {
            return new IsAborted { Value = false };
        }
        return React(ref args, ref target, ctx);
    }

    protected virtual bool ShouldReact(TAction args, TTarget target, Context ctx)
    {
        return true;
    }
    
    protected abstract IsAborted React(ref TAction args, ref TTarget target, Context ctx);
    
    public struct IsAborted
    {
        public bool Value { get; set; }
    }
}
