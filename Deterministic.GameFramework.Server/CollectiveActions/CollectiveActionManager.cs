using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.Core.Extensions;

namespace Deterministic.GameFramework.Server.CollectiveActions;

public class CollectiveActionManager : LeafDomain
{
    private readonly Dictionary<string, HashSet<Guid>> _waitForAllSubmissions = new();
    private readonly Dictionary<string, IWaitForAllAction> _waitForAllActions = new();
    private readonly Dictionary<string, HashSet<Guid>> _waitForAllRequiredPlayers = new();
    
    private readonly Dictionary<string, Dictionary<string, HashSet<Guid>>> _voteSubmissions = new();
    private readonly Dictionary<string, Dictionary<string, IVoteForAction>> _voteActions = new();
    private readonly Dictionary<string, HashSet<Guid>> _voteRequiredPlayers = new();
    
    private readonly Func<IEnumerable<Guid>> _getPlayerIds;
    
    public event Action<IWaitForAllAction>? OnWaitForAllReady;
    public event Action<IVoteForAction, Dictionary<string, int>>? OnVoteResolved;
    
    public CollectiveActionManager(BranchDomain parent, Func<IEnumerable<Guid>> getPlayerIds) : base(parent)
    {
        _getPlayerIds = getPlayerIds;
        
        new Reaction<LeafDomain, IRequireCollectiveActionManager>(parent)
            .Prepare((_, action) => action.CollectiveActionManager = this)
            .AddTo(Disposables);
    }
    
    public bool SubmitWaitForAll(IWaitForAllAction action)
    {
        var playerId = action.ExecutorId;
        if (playerId == null) return false;
        
        var key = action.CollectiveKey;
        
        if (!_waitForAllSubmissions.ContainsKey(key))
        {
            _waitForAllSubmissions[key] = new HashSet<Guid>();
            _waitForAllActions[key] = action;
            _waitForAllRequiredPlayers[key] = _getPlayerIds().ToHashSet();
        }
        
        var requiredPlayers = _waitForAllRequiredPlayers[key];
        if (!requiredPlayers.Contains(playerId.Value))
        {
            Console.WriteLine($"[CollectiveActionManager] Player {playerId} not in required players for {key}");
            return false;
        }
        
        _waitForAllSubmissions[key].Add(playerId.Value);
        
        var submittedIds = _waitForAllSubmissions[key];
        if (requiredPlayers.All(id => submittedIds.Contains(id)))
        {
            var actionToExecute = _waitForAllActions[key];
            _waitForAllSubmissions.Remove(key);
            _waitForAllActions.Remove(key);
            _waitForAllRequiredPlayers.Remove(key);
            
            OnWaitForAllReady?.Invoke(actionToExecute);
            return true;
        }
        
        return false;
    }
    
    public bool SubmitVote(IVoteForAction action)
    {
        var playerId = action.ExecutorId;
        if (playerId == null) return false;
        
        var key = action.CollectiveKey;
        var option = action.VoteOption;
        
        if (!_voteSubmissions.ContainsKey(key))
        {
            _voteSubmissions[key] = new Dictionary<string, HashSet<Guid>>();
            _voteActions[key] = new Dictionary<string, IVoteForAction>();
            _voteRequiredPlayers[key] = _getPlayerIds().ToHashSet();
        }
        
        var requiredPlayers = _voteRequiredPlayers[key];
        if (!requiredPlayers.Contains(playerId.Value))
        {
            Console.WriteLine($"[CollectiveActionManager] Player {playerId} not in required players for vote {key}");
            return false;
        }
        
        foreach (var optionVotes in _voteSubmissions[key].Values)
        {
            optionVotes.Remove(playerId.Value);
        }
        
        if (!_voteSubmissions[key].ContainsKey(option))
        {
            _voteSubmissions[key][option] = new HashSet<Guid>();
            _voteActions[key][option] = action;
        }
        _voteSubmissions[key][option].Add(playerId.Value);
        
        var allVoters = _voteSubmissions[key].Values.SelectMany(v => v).ToHashSet();
        if (requiredPlayers.All(id => allVoters.Contains(id)))
        {
            var voteCounts = _voteSubmissions[key]
                .ToDictionary(kv => kv.Key, kv => kv.Value.Count);
            
            var winningOption = voteCounts
                .OrderByDescending(kv => kv.Value)
                .First()
                .Key;
            
            var winningAction = _voteActions[key][winningOption];
            
            _voteSubmissions.Remove(key);
            _voteActions.Remove(key);
            _voteRequiredPlayers.Remove(key);
            
            winningAction.TriggerWinningExecution(voteCounts);
            OnVoteResolved?.Invoke(winningAction, voteCounts);
            return true;
        }
        
        return false;
    }
    
    public (int submitted, int total) GetWaitForAllStatus(string collectiveKey)
    {
        var total = _waitForAllRequiredPlayers.TryGetValue(collectiveKey, out var required) 
            ? required.Count 
            : _getPlayerIds().Count();
        var submitted = _waitForAllSubmissions.TryGetValue(collectiveKey, out var set) ? set.Count : 0;
        return (submitted, total);
    }
    
    public Dictionary<string, int> GetVoteCounts(string collectiveKey)
    {
        if (!_voteSubmissions.TryGetValue(collectiveKey, out var submissions))
            return new Dictionary<string, int>();
        
        return submissions.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
    }
    
    public bool HasSubmitted(string collectiveKey, Guid playerId)
    {
        return _waitForAllSubmissions.TryGetValue(collectiveKey, out var set) && set.Contains(playerId);
    }
    
    public bool HasVoted(string collectiveKey, Guid playerId)
    {
        if (!_voteSubmissions.TryGetValue(collectiveKey, out var submissions))
            return false;
        
        return submissions.Values.Any(voters => voters.Contains(playerId));
    }
    
    public void CancelCollectiveAction(string collectiveKey)
    {
        _waitForAllSubmissions.Remove(collectiveKey);
        _waitForAllActions.Remove(collectiveKey);
        _waitForAllRequiredPlayers.Remove(collectiveKey);
        _voteSubmissions.Remove(collectiveKey);
        _voteActions.Remove(collectiveKey);
        _voteRequiredPlayers.Remove(collectiveKey);
    }
}
