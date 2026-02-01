using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Provides match identification. Automatically sets MatchId on network actions.
/// </summary>
public class MatchIdDomain : BranchDomain
{
    public Guid MatchId { get; }
    
    public MatchIdDomain(BranchDomain parent, Guid matchId) : base(parent)
    {
        MatchId = matchId;
        
        // Auto-set MatchId on all network actions
        new Reaction<LeafDomain, INetworkAction>(parent)
            .Prepare((_, action) => action.MatchId = MatchId)
            .AddTo(Disposables);
    }
}
