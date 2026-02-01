using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Generic client-side root domain that manages game state and networking.
/// Provides a standard pattern for client applications.
/// </summary>
public class ClientDomain<TGameState> : RootDomain where TGameState : NetworkGameState
{
    public static ClientDomain<TGameState>? Instance { get; protected set; }
    
    public Guid UserId { get; }
    public Guid MatchId { get; }
    public GameLoop GameLoop { get; }
    public NetworkSyncManager NetworkSyncManager { get; }
    public TGameState GameState { get; }
    
    public ClientDomain(Guid userId, Guid matchId, TGameState gameState)
    {
        Instance = this;
        UserId = userId;
        MatchId = matchId;
        
        // Create game loop to process NetworkSyncManager
        GameLoop = new GameLoop(this);
        GameLoop.SetTargetFps(60);
        _ = GameLoop.Start();
        
        // Create network sync manager to handle action broadcasting
        NetworkSyncManager = new NetworkSyncManager(this);
        
        // Add game state as a subdomain
        GameState = gameState;
        Subdomains.Add(GameState);
    }
    
    /// <summary>
    /// Send an action to the server.
    /// Automatically sets ExecutorId and queues for network transmission.
    /// </summary>
    public void Send(INetworkAction action)
    {
        action.ExecutorId = UserId;
        new SendAction(action, GameState).Execute(this);
    }
}
