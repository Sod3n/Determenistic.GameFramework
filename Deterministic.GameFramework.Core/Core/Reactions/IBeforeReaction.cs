namespace Deterministic.GameFramework.Core;

/// <summary>
/// Marker interface for before reactions with non-generic invoke method.
/// </summary>
public interface IHasBeforeReaction : IReaction 
{ 
    void InvokeBefore(IDomain domain, IDARAction action);
}

/// <summary>
/// Interface for reactions that execute before an action (after abort check passes).
/// </summary>
public interface IBeforeReaction<TDomain, TAction> : IHasBeforeReaction
    where TDomain : IDomain
    where TAction : IDARAction
{
    void OnBefore(TDomain domain, TAction action);
    
    void IHasBeforeReaction.InvokeBefore(IDomain domain, IDARAction action)
    {
        if (action is TAction typedAction && domain is TDomain typedDomain)
        {
            OnBefore(typedDomain, typedAction);
        }
    }
}
