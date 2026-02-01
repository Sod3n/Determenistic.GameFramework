namespace Deterministic.GameFramework.Server;

/// <summary>
/// A MatchManager wrapper that creates shadow game states for determinism validation.
/// When DeterminismValidator.IsEnabled is true, each match gets a shadow copy.
/// </summary>
public class DeterminismValidatingMatchManager<TGameState> where TGameState : NetworkGameState
{
    private readonly MatchManager<TGameState> _innerManager;
    private readonly IGameStateFactory<TGameState> _gameStateFactory;
    private readonly Dictionary<Guid, DeterminismValidator<TGameState>> _validators = new();
    private readonly object _lock = new();
    
    /// <summary>
    /// Gets the validator for a specific match, if determinism validation is enabled.
    /// </summary>
    public DeterminismValidator<TGameState>? GetValidator(Guid matchId)
    {
        lock (_lock)
        {
            return _validators.GetValueOrDefault(matchId);
        }
    }
    
    public DeterminismValidatingMatchManager(
        MatchManager<TGameState> innerManager, 
        IGameStateFactory<TGameState> gameStateFactory)
    {
        _innerManager = innerManager;
        _gameStateFactory = gameStateFactory;
        
        // Hook into match creation
        _innerManager.OnMatchCreated += OnMatchCreated;
    }
    
    private void OnMatchCreated(Guid matchId, TGameState primary)
    {
        if (!DeterminismValidator<TGameState>.IsEnabled)
        {
            return;
        }
        
        lock (_lock)
        {
            // Create shadow game state with same matchId (same random seed)
            // Shadow is NOT added to server domain - it's isolated
            var shadow = _gameStateFactory.CreateGameState(matchId);
            
            // Note: Shadow starts with same matchId (same random seed) so initial state should match
            // Validation happens during action execution via ExecutionLogger
            Console.WriteLine($"[DeterminismValidatingMatchManager] Primary history count: {primary.History.Count}");
            
            if (primary.History.Count > 0)
            {
                Console.WriteLine($"[DeterminismValidatingMatchManager] WARNING: Match created with existing history.");
                Console.WriteLine($"  Shadow will start from initial state, not synced with primary history.");
                Console.WriteLine($"  Validation will only work for actions executed after this point.");
            }
            
            var validator = new DeterminismValidator<TGameState>(primary, shadow, matchId);
            _validators[matchId] = validator;
            
            Console.WriteLine($"[DeterminismValidatingMatchManager] Shadow created for match {matchId}");
        }
    }
    
    /// <summary>
    /// Removes the validator when a match is removed.
    /// </summary>
    public void OnMatchRemoved(Guid matchId)
    {
        lock (_lock)
        {
            if (_validators.Remove(matchId, out var validator))
            {
                validator.Shadow.Dispose();
                Console.WriteLine($"[DeterminismValidatingMatchManager] Validator removed for match {matchId}");
            }
        }
    }
    
    /// <summary>
    /// Gets summary of all validators.
    /// </summary>
    public IEnumerable<(Guid MatchId, int ActionCount, bool HasFailed)> GetValidatorSummaries()
    {
        lock (_lock)
        {
            return _validators.Select(kv => (kv.Key, kv.Value.ActionCount, kv.Value.HasFailed)).ToList();
        }
    }
    
    /// <summary>
    /// Install validation hook on a NetworkActionExecutor for a specific match.
    /// Call this when creating an executor for a match that has validation enabled.
    /// </summary>
    public void InstallValidationHook(Guid matchId, NetworkActionExecutor executor)
    {
        var validator = GetValidator(matchId);
        if (validator == null) return;
        
        executor.ValidationHook = (action, executePrimary) =>
        {
            // Skip SyncGameStateAction to avoid recursion
            if (action.GetType().Name == "SyncGameStateAction")
            {
                executePrimary();
                return true;
            }
            
            return validator.ValidateAction(action, executePrimary);
        };
    }
}
