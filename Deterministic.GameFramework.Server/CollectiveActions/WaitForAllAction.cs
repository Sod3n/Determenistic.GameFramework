using System;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Server.CollectiveActions;

public abstract class WaitForAllAction<TDomain, TAction> : NetworkAction<TDomain, TAction>, IWaitForAllAction, IRequireCollectiveActionManager 
    where TDomain : class, IDomain 
    where TAction : class, INetworkAction
{
    public virtual string CollectiveKey => GetType().Name;
    
    [JsonIgnore] public CollectiveActionManager? CollectiveActionManager { get; set; }
    [JsonIgnore] public bool IsCollectiveExecution { get; set; }
    
    protected sealed override void ExecuteProcess(TDomain domain)
    {
        if (CollectiveActionManager == null)
        {
            Console.WriteLine($"[WaitForAllAction] No CollectiveActionManager injected for {GetType().Name}");
            return;
        }
        
        if (CollectiveActionManager.SubmitWaitForAll(this))
        {
            IsCollectiveExecution = true;
            ExecuteWhenReady(domain);
        }
        else
        {
            OnPlayerSubmitted(domain);
        }
    }
    
    protected abstract void ExecuteWhenReady(TDomain domain);
    
    protected virtual void OnPlayerSubmitted(TDomain domain)
    {
        Console.WriteLine($"[WaitForAllAction] Player {ExecutorId} submitted {GetType().Name}");
    }
}
