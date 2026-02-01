namespace Deterministic.GameFramework.Core;

/// <summary>
/// Marker interface for prepare reactions with non-generic invoke method.
/// </summary>
public interface IHasPrepareReaction : IReaction 
{ 
    void InvokePrepare(IDomain domain, IDARAction action);
}

/// <summary>
/// Interface for reactions that execute during the prepare phase (before abort check).
/// Use this to modify action parameters before execution is validated.
/// </summary>
public interface IPrepareReaction<TDomain, TAction> : IHasPrepareReaction
    where TDomain : IDomain
    where TAction : IDARAction
{
    void OnPrepare(TDomain domain, TAction action);
    
    void IHasPrepareReaction.InvokePrepare(IDomain domain, IDARAction action)
    {
        if (action is TAction typedAction && domain is TDomain typedDomain)
        {
            OnPrepare(typedDomain, typedAction);
        }
    }
}
