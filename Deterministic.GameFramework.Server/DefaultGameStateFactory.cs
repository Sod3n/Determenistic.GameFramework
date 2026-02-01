namespace Deterministic.GameFramework.Server;

/// <summary>
/// Default factory that uses a delegate to create game state instances.
/// No reflection - just a simple function call.
/// </summary>
public class DefaultGameStateFactory<TGameState> : IGameStateFactory<TGameState> 
    where TGameState : NetworkGameState
{
    private readonly Func<Guid, TGameState> _factory;
    
    public DefaultGameStateFactory(Func<Guid, TGameState> factory)
    {
        _factory = factory;
    }
    
    public TGameState CreateGameState(Guid matchId)
    {
        return _factory(matchId);
    }
}
