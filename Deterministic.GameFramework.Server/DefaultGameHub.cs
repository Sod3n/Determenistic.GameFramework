namespace Deterministic.GameFramework.Server;

/// <summary>
/// Default GameHub implementation that works out of the box.
/// No need to create a custom hub class unless you need custom functionality.
/// </summary>
public class DefaultGameHub<TMatchData, TGameState> : GameHub<TMatchData, MatchManager<TMatchData, TGameState>, TGameState> 
    where TGameState : NetworkGameState
{
    public DefaultGameHub(ServerDomain serverDomain, MatchManager<TMatchData, TGameState> matchManager) 
        : base(serverDomain, matchManager)
    {
    }
}
