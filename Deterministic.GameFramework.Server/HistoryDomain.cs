using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Tracks history of network actions for replay/debugging.
/// </summary>
public class HistoryDomain : BranchDomain
{
    public List<INetworkAction> History { get; } = new();
    
    public HistoryDomain(BranchDomain parent) : base(parent)
    {
        // Record all network actions
        new Reaction<LeafDomain, INetworkAction>(parent)
            .After((_, action) => History.Add(action))
            .AddTo(Disposables);
    }
    
    public void Clear() => History.Clear();
}
