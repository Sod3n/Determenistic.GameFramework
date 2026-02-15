using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Network;
using Deterministic.GameFramework.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Deterministic.GameFramework.Examples.Network;

/// <summary>
/// Example 9: Matchmaking
/// Demonstrates MatchManager, IGameStateFactory, custom hubs, and match ID strategies.
/// See articles/network/07-matchmaking.md for the full explanation.
/// </summary>
public static class Example09_Matchmaking
{
    // ========================================================================
    // CUSTOM GAME STATE FACTORY
    // ========================================================================
    
    /// <summary>
    /// Custom factory that injects configuration into new game states.
    /// Use this when game states need external data (configs, balancing, etc.)
    /// For simple cases, use DefaultGameStateFactory with a delegate instead.
    /// </summary>
    public class BattleGameStateFactory : IGameStateFactory<Guid, BattleGameState>
    {
        private readonly GameConfig _config;
        
        public BattleGameStateFactory(GameConfig config)
        {
            _config = config;
        }
        
        public BattleGameState CreateGameState(Guid matchId)
        {
            var state = new BattleGameState(matchId, randomSeed: matchId.GetHashCode());
            state.ApplyConfig(_config);
            return state;
        }
    }
    
    // ========================================================================
    // CUSTOM GAME HUB
    // ========================================================================
    
    /// <summary>
    /// Custom hub that extends GameHub with game-specific connection logic.
    /// Override OnClientConnected to assign player slots, notify others, etc.
    /// For default behavior, use DefaultGameHub instead.
    /// </summary>
    public class BattleGameHub : GameHub<Guid, MatchManager<Guid, BattleGameState>, BattleGameState>
    {
        public BattleGameHub(ServerDomain serverDomain, MatchManager<Guid, BattleGameState> matchManager) 
            : base(serverDomain, matchManager) { }
        
        protected override async Task OnClientConnected(Guid userId, Guid matchId)
        {
            Console.WriteLine($"[BattleGameHub] Player {userId} joined match {matchId}");
            // Custom logic: assign player slot, send welcome message, etc.
            await base.OnClientConnected(userId, matchId);
        }
    }
    
    // ========================================================================
    // MATCH ID STRATEGIES
    // ========================================================================
    
    /// <summary>
    /// Strategy 1: Shared room code.
    /// One player creates a matchId, shares it with others (lobby, invite link, etc.)
    /// </summary>
    public static Guid CreateRoomCode()
    {
        var matchId = Guid.NewGuid();
        // Share this matchId with other players via lobby, chat, invite link, etc.
        // All players connect with: ?userId={userId}&matchId={matchId}
        return matchId;
    }
    
    /// <summary>
    /// Strategy 2: External matchmaker.
    /// A separate service pairs players and assigns a shared matchId.
    /// </summary>
    public static Guid AssignMatchFromMatchmaker(Guid playerA, Guid playerB)
    {
        var matchId = Guid.NewGuid();
        // Notify both players of the matchId (e.g., via push notification, polling, etc.)
        // Both players then connect with: ?userId={userId}&matchId={matchId}
        Console.WriteLine($"[Matchmaker] Assigned match {matchId} to {playerA} and {playerB}");
        return matchId;
    }
    
    /// <summary>
    /// Strategy 3: Deterministic match ID.
    /// Derive matchId from inputs so the same pairing always produces the same seed.
    /// Useful for ranked/seeded matches and deterministic replay.
    /// </summary>
    public static Guid DeterministicMatchId(Guid playerA, Guid playerB, long timestamp)
    {
        var input = $"{playerA}_{playerB}_{timestamp}";
        // Deterministic GUID from string — same inputs always produce the same matchId
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
    
    // ========================================================================
    // SERVER SETUP EXAMPLES
    // ========================================================================
    
    /// <summary>
    /// Minimal server setup using AddMultiplayerServer with a delegate factory.
    /// This is the simplest way to get a multiplayer server running.
    /// </summary>
    public static void MinimalServerSetup(string[] args)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
        
        builder.Services.AddMultiplayerServer<Guid, BattleGameState>(
            matchId => new BattleGameState(matchId, randomSeed: matchId.GetHashCode())
        );
        
        var app = builder.Build();
        app.MapHub<DefaultGameHub<Guid, BattleGameState>>("/gamehub");
        app.Run();
    }
    
    /// <summary>
    /// Server setup with custom factory and custom hub.
    /// Use this when you need custom game state initialization or connection logic.
    /// </summary>
    public static void CustomServerSetup(string[] args)
    {
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder(args);
        
        builder.Services.AddMultiplayerServer<Guid, BattleGameState, BattleGameStateFactory>();
        
        var app = builder.Build();
        app.MapHub<BattleGameHub>("/gamehub");
        app.Run();
    }
    
    // ========================================================================
    // CLIENT CONNECTION EXAMPLE
    // ========================================================================
    
    /// <summary>
    /// Client-side connection to a match.
    /// The userId and matchId are passed as query parameters.
    /// </summary>
    public static async Task ConnectClient(Guid userId, Guid matchId)
    {
        var connection = new Microsoft.AspNetCore.SignalR.Client.HubConnectionBuilder()
            .WithUrl($"http://localhost:5000/gamehub?userId={userId}&matchId={matchId}")
            .Build();
        
        // Listen for state sync and action broadcasts
        connection.On<string>("SyncActions", actionsJson =>
        {
            Console.WriteLine($"[Client] Received actions: {actionsJson.Length} chars");
        });
        
        await connection.StartAsync();
        Console.WriteLine($"[Client] Connected as {userId} to match {matchId}");
    }
}

// ========================================================================
// SUPPORTING TYPES
// ========================================================================

public class BattleGameState : NetworkGameState
{
    public GameConfig? Config { get; private set; }
    
    public BattleGameState(Guid matchId, int randomSeed) : base(matchId, randomSeed) { }
    
    public void ApplyConfig(GameConfig config)
    {
        Config = config;
    }
}

public class GameConfig
{
    public int MaxPlayers { get; set; } = 4;
    public string Difficulty { get; set; } = "Normal";
}
