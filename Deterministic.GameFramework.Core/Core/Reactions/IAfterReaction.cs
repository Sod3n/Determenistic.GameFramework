namespace Deterministic.GameFramework.Core;

/// <summary>
/// Marker interface for after reactions with non-generic invoke method.
/// </summary>
public interface IHasAfterReaction : IReaction 
{ 
    void InvokeAfter(IDomain domain, IDARAction action);
}

/// <summary>
/// Interface for reactions that execute after an action completes.
/// </summary>
public interface IAfterReaction<TDomain, TAction> : IHasAfterReaction
    where TDomain : IDomain
    where TAction : IDARAction
{
    void OnAfter(TDomain domain, TAction action);
    
    void IHasAfterReaction.InvokeAfter(IDomain domain, IDARAction action)
    {
        if (action is TAction typedAction && domain is TDomain typedDomain)
        {
            OnAfter(typedDomain, typedAction);
        }
    }
}
