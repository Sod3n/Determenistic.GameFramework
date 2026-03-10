using Deterministic.GameFramework.Server;

namespace MultiplayerHelloWorld.Shared;

/// <summary>
/// Client domain for the counter game.
/// Inherits from the generic ClientDomain in Network project.
/// </summary>
public class ClientDomain : ClientDomain<CounterGameState>
{
    public ClientDomain(Guid userId, Guid matchId) 
        : base(userId, matchId, new CounterGameState(matchId))
    {
    }
}
