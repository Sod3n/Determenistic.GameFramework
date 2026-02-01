using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterministic.GameFramework.Core.Domain;
using JsonSerializer = Deterministic.GameFramework.Core.Utils.JsonSerializer;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Global NetworkSyncManager that monitors all GameStates in the ServerDomain.
/// Groups actions by matchId and broadcasts to appropriate match groups.
/// </summary>
public class NetworkSyncManager : BranchDomain, IProcessor
{
    // Pending actions grouped by matchId
    private Dictionary<Guid, List<INetworkAction>> _pendingActionsByMatch = new();
    private float _timeSinceLastSync = 0f;
    private float _syncInterval = 0.05f; // 20Hz sync rate (50ms)
    
    // Event for sending actions - (matchId, actions)
    // Consumer can filter actions per-client before serializing
    public event Action<Guid, List<INetworkAction>>? OnSync;

    /// <summary>
    /// Creates a global NetworkSyncManager that monitors all GameStates.
    /// </summary>
    /// <param name="serverDomain">The server domain containing all GameStates</param>
    /// <param name="syncInterval">Sync interval in seconds (default 50ms = 20Hz)</param>
    public NetworkSyncManager(BranchDomain serverDomain, float syncInterval = 0.05f) : base(serverDomain)
    {
        _syncInterval = syncInterval;
    }
    
    // IProcessor implementation - syncs on fixed interval
    public void Process(float delta)
    {
        _timeSinceLastSync += delta;
        
        // Sync at fixed interval if we have pending actions
        if (_timeSinceLastSync >= _syncInterval)
        {
            Flush();
        }
    }
    
    public void Flush()
    {
        // Take snapshot and clear immediately to avoid issues if OnSync triggers new actions
        var snapshot = _pendingActionsByMatch;
        _pendingActionsByMatch = new Dictionary<Guid, List<INetworkAction>>();
        _timeSinceLastSync = 0f;
        
        // Broadcast actions for each match separately
        foreach (var (matchId, actions) in snapshot)
        {
            if (actions.Count > 0)
            {
                // Invoke event with action list
                // Consumer can filter per-client and serialize
                OnSync?.Invoke(matchId, actions);
            }
        }
    }

    public void AddPendingAction(LeafDomain domain, INetworkAction action)
    {
        action.DomainId = domain.Id;
        var matchId = action.MatchId;
        if (!_pendingActionsByMatch.ContainsKey(matchId))
        {
            _pendingActionsByMatch[matchId] = new List<INetworkAction>();
        }
        _pendingActionsByMatch[matchId].Add(action);
    }

    public class CollectNetworkActionsReaction(NetworkSyncManager manager, ServerDomain target) : Reaction(target), IAfterReaction<BranchDomain, INetworkAction>
    {
        
        public void OnAfter(BranchDomain domain, INetworkAction action)
        {
            if (!action.SyncToClient) return;
            manager.AddPendingAction(domain, action);
        }
    }
}