using System;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Server;

public interface INetworkAction : IDARAction
{
    Guid? ExecutorId { get; set; }
    Guid MatchId { get; set; }
    int DomainId { get; set; }
    bool IsServer { get; set; }
    NetworkThread Thread { get; }
    int CurrentId { get; set; }
    bool SyncToClient { get; set; }
}


public abstract class NetworkAction<TDomain, TAction> : DARAction<TDomain, TAction>, INetworkAction where TDomain : class, IDomain where TAction : class, INetworkAction
{
    public Guid? ExecutorId { get; set; }
    public Guid MatchId { get; set; }
    public int DomainId { get; set; }
    public bool IsServer { get; set; }
    public bool SyncToClient { get; set; }
    public virtual NetworkThread Thread { get; protected set; } = NetworkThread.Main;
    public int CurrentId { get; set; }    
    /// <summary>
    /// If true, this action contains server secrets and should NOT be sent to clients.
    /// Only the result/effect should be visible to clients.
    /// </summary>
    [JsonIgnore]
    public virtual bool IsServerSecret { get; protected set; } = false;
}