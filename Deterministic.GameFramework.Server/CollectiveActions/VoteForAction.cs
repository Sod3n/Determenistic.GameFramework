using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Server.CollectiveActions;

public abstract class VoteForAction<TDomain, TAction> : NetworkAction<TDomain, TAction>, IVoteForAction, IRequireCollectiveActionManager 
    where TDomain : class, IDomain 
    where TAction : class, INetworkAction
{
    public abstract string CollectiveKey { get; }
    public abstract string VoteOption { get; }
    
    [JsonIgnore] public CollectiveActionManager? CollectiveActionManager { get; set; }
    [JsonIgnore] public bool IsWinningExecution { get; set; }
    [JsonIgnore] public Dictionary<string, int>? FinalVoteCounts { get; set; }
    [JsonIgnore] private TDomain? _executionDomain;
    
    protected sealed override void ExecuteProcess(TDomain domain)
    {
        if (CollectiveActionManager == null)
        {
            Console.WriteLine($"[VoteForAction] No CollectiveActionManager injected for {GetType().Name}");
            return;
        }
        
        _executionDomain = domain;
        OnVoteSubmitted(domain);
        CollectiveActionManager.SubmitVote(this);
    }
    
    protected abstract void ExecuteWhenWon(TDomain domain);
    
    protected virtual void OnVoteSubmitted(TDomain domain)
    {
        Console.WriteLine($"[VoteForAction] Player {ExecutorId} voted for {VoteOption} in {CollectiveKey}");
    }
    
    public void TriggerWinningExecution(Dictionary<string, int> voteCounts)
    {
        IsWinningExecution = true;
        FinalVoteCounts = voteCounts;
        if (_executionDomain != null)
            ExecuteWhenWon(_executionDomain);
    }
}
