using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Convenience base class that bundles common game state components.
/// For more control, use RootDomain and add individual components.
/// </summary>
public class NetworkGameState : BranchDomain
{
	public MatchIdDomain MatchIdDomain { get; }
	public DomainRegistry Registry { get; }
	public HistoryDomain HistoryDomain { get; }
	public RandomProviderDomain RandomProviderDomain { get; }
	public IdProviderDomain IdProvider { get; }
	
	// Convenience accessors
	public Guid MatchId => MatchIdDomain.MatchId;
	public List<INetworkAction> History => HistoryDomain.History;
	
	protected NetworkGameState(Guid matchId, int randomSeed) : base(null)
	{
		MatchIdDomain = new MatchIdDomain(this, matchId);
		Registry = new DomainRegistry(this);
		HistoryDomain = new HistoryDomain(this);
		RandomProviderDomain = new RandomProviderDomain(this, randomSeed);
		IdProvider = new IdProviderDomain(this);
	}
	
	public LeafDomain? GetDomain(int id) => Registry.GetDomain(id);
	public bool TryGetDomain(int id, out LeafDomain domain) => Registry.TryGetDomain(id, out domain);
}