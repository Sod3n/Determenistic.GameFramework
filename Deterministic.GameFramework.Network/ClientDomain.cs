using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Network;

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
        // TickDelay gives server actions time to arrive before the client needs them
        GameLoop = new GameLoop(this);
        GameLoop.SetTargetFps(60);
        GameLoop.TickDelay = 3;
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
    
    /// <summary>
    /// Connect to a server using the provided transport.
    /// Wires up sending and receiving of network actions automatically.
    /// </summary>
    public async Task ConnectAsync(INetworkTransport transport)
    {
        // Wire up NetworkSyncManager to send actions via transport
        NetworkSyncManager.OnSync += (matchId, actions) =>
        {
            var json = JsonSerializer.ToJson(actions);
            transport.Send("SyncActions", json);
        };
        
        // Handle incoming actions from server - schedule on the correct tick
        transport.On("SyncActions", actionsJson =>
        {
            GameLoop.Schedule(() =>
            {
                var actions = JsonSerializer.FromJson<List<INetworkAction>>(actionsJson);
                if (actions == null || actions.Count == 0) return;
                
                // Group actions by tick and schedule them
                foreach (var action in actions)
                {
                    var tick = action.Tick;
                    if (tick > 0 && tick > GameLoop.CurrentTick)
                    {
                        // Future tick - schedule for that exact tick
                        GameLoop.ScheduleOnTick(tick, () =>
                        {
                            var executor = new NetworkActionExecutor(GameState.Registry);
                            executor.ExecuteAction(action);
                        });
                    }
                    else
                    {
                        // Tick already passed or unset - execute immediately
                        var executor = new NetworkActionExecutor(GameState.Registry);
                        executor.ExecuteAction(action);
                    }
                }
            });
        });
        
        // Connect
        await transport.ConnectAsync();
    }
}
