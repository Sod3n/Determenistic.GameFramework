namespace Deterministic.GameFramework.Network;

/// <summary>
/// Default factory that uses a delegate to create game state instances.
/// No reflection - just a simple function call.
/// </summary>
public class DefaultGameStateFactory<TMatchData, TGameState> : IGameStateFactory<TMatchData, TGameState> 
    where TGameState : NetworkGameState
{
    private readonly Func<TMatchData, TGameState> _factory;
    
    public DefaultGameStateFactory(Func<TMatchData, TGameState> factory)
    {
        _factory = factory;
    }
    
    public TGameState CreateGameState(TMatchData matchData)
    {
        return _factory(matchData);
    }
}
