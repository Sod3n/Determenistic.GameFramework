using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Base SignalR hub for game communication using DAR architecture.
/// This is a partial class - action files can add their own hub methods.
/// </summary>
public partial class GameHub<TMatchManager, TGameState> : Hub 
    where TMatchManager : MatchManager<TGameState>
    where TGameState : NetworkGameState
{
    // Injected dependencies
    protected readonly ServerDomain ServerDomain;
    protected readonly TMatchManager MatchManager;
    
    // Connection tracking: ConnectionId -> (PlayerId, MatchId) - thread-safe for concurrent access
    private static readonly ConcurrentDictionary<string, (Guid PlayerId, Guid MatchId)> Connections = new();
    
    // Track player count per match for cleanup
    private static readonly ConcurrentDictionary<Guid, int> MatchPlayerCounts = new();
    
    protected GameHub(ServerDomain serverDomain, TMatchManager matchManager)
    {
        ServerDomain = serverDomain;
        MatchManager = matchManager;
    }
    
    // ============================================================================
    // CONNECTION LIFECYCLE
    // ============================================================================
    
    public override async Task OnConnectedAsync()
    {
        // Get connection parameters from query string
        var httpContext = Context.GetHttpContext();
        if (httpContext == null)
        {
            Context.Abort();
            return;
        }
        
        var userId = Guid.Parse(httpContext.Request.Query["userId"]);
        var matchId = Guid.Parse(httpContext.Request.Query["matchId"]);
        
        // Store connection mapping
        Connections[Context.ConnectionId] = (userId, matchId);
        
        // Create match if it doesn't exist
        var gameState = MatchManager.GetMatch(matchId);
        if (gameState == null)
        {
            gameState = MatchManager.CreateMatch(matchId);
            Console.WriteLine($"[GameHub] Created new match {matchId}");
        }
        
        // Increment player count for this match
        MatchPlayerCounts.AddOrUpdate(matchId, 1, (_, count) => count + 1);
        
        // Add to SignalR group for this match
        await Groups.AddToGroupAsync(Context.ConnectionId, matchId.ToString());
        
        var playerCount = MatchPlayerCounts.GetValueOrDefault(matchId, 0);
        Console.WriteLine($"[GameHub] Player {userId} connected to match {matchId} (players: {playerCount})");
        
        await base.OnConnectedAsync();
        
        // Send initial game state sync to newly connected client
        await OnClientConnected(userId, matchId);
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Get connection info
        if (Connections.TryRemove(Context.ConnectionId, out var connection))
        {
            var (userId, matchId) = connection;
            
            // Remove from SignalR group
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, matchId.ToString());
            
            // Decrement player count and check if match should be removed
            var remainingPlayers = MatchPlayerCounts.AddOrUpdate(matchId, 0, (_, count) => Math.Max(0, count - 1));
            
            Console.WriteLine($"[GameHub] Player {userId} disconnected from match {matchId} (remaining: {remainingPlayers})");
            
            // If no players left, schedule match removal after a delay
            // This allows any pending actions to execute before the match is destroyed
            if (remainingPlayers == 0)
            {
                MatchPlayerCounts.TryRemove(matchId, out _);
                
                // Delay removal to allow GameLoop to process any scheduled actions
                _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ =>
                {
                    MatchManager.RemoveMatch(matchId);
                    Console.WriteLine($"[GameHub] Match {matchId} removed - no players remaining");
                });
            }
        }
        
        await base.OnDisconnectedAsync(exception);
    }
    
    // ============================================================================
    // HELPER METHODS
    // ============================================================================
    
    /// <summary>
    /// Called when a client connects. Automatically sends state sync if match exists.
    /// Override to add custom connection logic.
    /// </summary>
    protected virtual async Task OnClientConnected(Guid userId, Guid matchId)
    {
        // Automatically send state sync to new/reconnecting clients
        var gameState = MatchManager.GetMatch(matchId);
        if (gameState != null && gameState is TGameState typedState)
        {
            await SendStateSyncToClient(typedState, userId);
        }
    }
    
    /// <summary>
    /// Sends full state sync to a specific client using history replay.
    /// Called automatically on connection, can also be called manually for refresh.
    /// </summary>
    protected virtual async Task SendStateSyncToClient(TGameState gameState, Guid userId)
    {
        // Create sync action with full history
        var syncAction = new SyncGameStateAction<TGameState>(gameState);
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(new[] { syncAction });
        
        // Send only to the specific client
        var connectionId = Connections.FirstOrDefault(c => c.Value.PlayerId == userId).Key;
        if (connectionId != null)
        {
            await Clients.Client(connectionId).SendAsync("SyncActions", json);
            Console.WriteLine($"[GameHub] Sent state sync to client {userId} ({gameState.History.Count} actions)");
        }
    }
    
    protected (Guid UserId, Guid MatchId) GetConnection()
    {
        return Connections[Context.ConnectionId];
    }
    
    protected void ScheduleAction(Action action)
    {
        ServerDomain.GameLoop.Schedule(action);
    }
    
    // ============================================================================
    // HUB METHODS
    // ============================================================================
    
    /// <summary>
    /// Simple ping/pong for latency measurement.
    /// Client sends Ping, server responds with Pong containing the same timestamp.
    /// </summary>
    public Task Ping(long clientTime)
    {
        return Clients.Caller.SendAsync("Pong", clientTime);
    }
    
    /// <summary>
    /// Time synchronization helper.
    /// Client sends its current UTC ticks, server responds with server UTC ticks.
    /// The client can then compute clock offset using NTP-style formula.
    /// </summary>
    public Task TimeSync(long clientTicks)
    {
        // Note: clientTicks is not used on the server; it's used only on the client
        // to compute offset together with local receive time.
        var serverTicks = DateTime.UtcNow.Ticks;
        return Clients.Caller.SendAsync("TimeSync", serverTicks);
    }
    
    /// <summary>
    /// Execute a batch of actions from client.
    /// Batch can contain one or more actions.
    /// Actions are self-registered via their static constructors.
    /// </summary>
    public void SyncActions(string actionsJson)
    {
        var (userId, matchId) = GetConnection();
        Console.WriteLine($"[GameHub] SyncActions called by user {userId}, json length: {actionsJson?.Length ?? 0}");
        Console.WriteLine($"[GameHub] JSON preview: {actionsJson?.Substring(0, Math.Min(200, actionsJson?.Length ?? 0))}");
        
        // Relay-only mode: just broadcast actions to all clients without simulation
        if (ServerDomain.RelayOnlyMode)
        {
            ScheduleAction(() =>
            {
                Clients.Group(matchId.ToString()).SendAsync("SyncActions", actionsJson);
                Console.WriteLine($"[GameHub] Relayed actions from user {userId} to match {matchId}");
            });
            return;
        }
        
        // Normal mode: simulate on server
        ScheduleAction(() =>
        {
            // TODO: Add validation here
            // if (!ValidateActions(actions, userId)) return;
            
            // Get the match's game state
            var gameState = MatchManager.GetMatch(matchId);
            if (gameState == null)
            {
                Console.WriteLine($"[GameHub] Match {matchId} not found");
                return;
            }
            
            // Create executor for this match
            // Pass userId to override ExecutorId for security - don't trust client-provided ExecutorId
            var executor = new NetworkActionExecutor(gameState.Registry);
            executor.BeforeAction += action => action.SyncToClient = true;
            var count = executor.ExecuteBatch(
                actionsJson,
                executorId: userId,
                onError: error => Console.WriteLine($"[GameHub] {error}")
            );
            
            if (count > 0)
            {
                Console.WriteLine($"[GameHub] Executed {count} action(s) from user {userId}");
            }
        });
    }
}
