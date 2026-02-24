
namespace Deterministic.GameFramework.Network;

/// <summary>
/// Manages all active matches in the server.
/// Thread-safe match creation, retrieval, and removal.
/// NetworkSyncManager is global and automatically handles all matches.
/// </summary>
public class MatchManager<TMatchData, TGameState> where TGameState : NetworkGameState
{
	private readonly ServerDomain _serverDomain;
	private readonly IGameStateFactory<TMatchData, TGameState> _gameStateFactory;
	private readonly Dictionary<Guid, TGameState> _matches = new();
	private readonly object _lock = new();
	
	// Event fired when a new match is created - (matchData, matchId, gameState)
	public event Action<TMatchData, Guid, TGameState>? OnMatchCreated;
	
	/// <summary>
	/// Creates a match manager.
	/// </summary>
	/// <param name="serverDomain">The server domain to add matches to</param>
	/// <param name="gameStateFactory">Factory for creating game state instances</param>
	public MatchManager(ServerDomain serverDomain, IGameStateFactory<TMatchData, TGameState> gameStateFactory)
	{
		_serverDomain = serverDomain;
		_gameStateFactory = gameStateFactory;
	}
	
	/// <summary>
	/// Creates a new match and adds it to the server domain.
	/// </summary>
	public TGameState CreateMatch(TMatchData matchData)
	{
		var match = _gameStateFactory.CreateGameState(matchData);
		var matchId = match.MatchId;
		
		lock (_lock)
		{
			if (_matches.ContainsKey(matchId))
			{
				throw new InvalidOperationException($"Match {matchId} already exists");
			}
			
			_matches[matchId] = match;
			_serverDomain.GameLoop.Schedule(() => _serverDomain.Subdomains.Add(match));
			
			// Fire event for match creation
			OnMatchCreated?.Invoke(matchData, matchId, match);
			
			Console.WriteLine($"[MatchManager] Match {matchId} registered");
			
			return match;
		}
	}
	
	/// <summary>
	/// Gets an existing match by ID.
	/// </summary>
	public TGameState? GetMatch(Guid matchId)
	{
		lock (_lock)
		{
			return _matches.GetValueOrDefault(matchId);
		}
	}
	
	/// <summary>
	/// Removes a match from the server domain.
	/// </summary>
	public void RemoveMatch(Guid matchId)
	{
		lock (_lock)
		{
			if (_matches.Remove(matchId, out var match))
			{
				// Remove from server domain (automatically stops processing)
				_serverDomain.GameLoop.Schedule(() =>
				{
					_serverDomain.Subdomains.Remove(match);
					match.Dispose(); // Explicit disposal after removal
				});
			}
		}
	}
	
	/// <summary>
	/// Gets all active match IDs.
	/// </summary>
	public IEnumerable<Guid> GetAllMatchIds()
	{
		lock (_lock)
		{
			return new List<Guid>(_matches.Keys);
		}
	}
	
	/// <summary>
	/// Gets the count of active matches.
	/// </summary>
	public int MatchCount
	{
		get
		{
			lock (_lock)
			{
				return _matches.Count;
			}
		}
	}
}
