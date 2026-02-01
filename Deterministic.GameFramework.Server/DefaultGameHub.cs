namespace Deterministic.GameFramework.Server;

/// <summary>
/// Default GameHub implementation that works out of the box.
/// No need to create a custom hub class unless you need custom functionality.
/// </summary>
public class DefaultGameHub<TGameState> : GameHub<MatchManager<TGameState>, TGameState> 
    where TGameState : NetworkGameState
{
    public DefaultGameHub(ServerDomain serverDomain, MatchManager<TGameState> matchManager) 
        : base(serverDomain, matchManager)
    {
    }
}
