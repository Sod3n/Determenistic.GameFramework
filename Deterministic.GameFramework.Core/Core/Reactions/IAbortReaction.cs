namespace Deterministic.GameFramework.Core;

/// <summary>
/// Marker interface for abort reactions with non-generic invoke method.
/// </summary>
public interface IHasAbortReaction : IReaction 
{ 
    /// <summary>
    /// Invokes the abort check. Returns true if the action should be aborted.
    /// </summary>
    bool InvokeAbort(IDomain domain, IDARAction action);
}

/// <summary>
/// Interface for reactions that can abort an action.
/// Return true from OnAbort to abort the action.
/// </summary>
public interface IAbortReaction<TDomain, TAction> : IHasAbortReaction
    where TDomain : IDomain
    where TAction : IDARAction
{
    /// <summary>
    /// Called to check if the action should be aborted.
    /// Return true to abort the action, false to allow it to proceed.
    /// </summary>
    bool OnAbort(TDomain domain, TAction action);
    
    bool IHasAbortReaction.InvokeAbort(IDomain domain, IDARAction action)
    {
        if (action is TAction typedAction && domain is TDomain typedDomain)
        {
            return OnAbort(typedDomain, typedAction);
        }
        return false;
    }
}
