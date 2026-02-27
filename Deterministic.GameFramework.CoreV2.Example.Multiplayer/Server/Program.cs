using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.ServerV2;
using Deterministic.GameFramework.NetworkV2.Server;
using Deterministic.GameFramework.CoreV2;
using Shared;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Game Framework Server services
builder.Services.AddSignalR();
builder.Services.AddSingleton<IAuthService, BasicAuthService>(); // Use basic auth for example
builder.Services.AddSingleton<IMatchFactory, HelloWorldMatchFactory>();
builder.Services.AddSingleton<MatchManager>();
builder.Services.AddSingleton<MatchBroadcaster>();

var app = builder.Build();

// 2. Configure a Match (this is where the "Plug and Play" logic lives)
var matchManager = app.Services.GetRequiredService<MatchManager>();

// Create a persistent match for newbies to join
var matchId = Guid.Parse("00000000-0000-0000-0000-000000000001");
matchManager.CreateMatch(matchId);

// 3. Map the GameHub
app.MapHub<GameHub>("/gamehub");

Console.WriteLine($"[Server] Started. Match ID: {matchId}");
app.Run();

// Minimal Factory for newbies to see how to wire up logic
public class HelloWorldMatchFactory : IMatchFactory
{
    public Match CreateMatch(Guid matchId)
    {
        var state = new GlobalState();
        var dispatcher = new Dispatcher();
        
        // Register our shared logic
        dispatcher.RegisterAction(new IncrementScoreService(), Array.Empty<ReactionService<IncrementScoreAction, PlayerComponent>>());
        dispatcher.RegisterAction(new SpawnPlayerService(), Array.Empty<ReactionService<SpawnPlayerAction, PlayerComponent>>());
        
        var scheduler = new ActionScheduler();
        var loop = new GameLoop(state, dispatcher, scheduler);
        
        // Create System Entity (0) for routing spawn requests
        var systemEntity = state.CreateEntity();
        state.AddComponent(systemEntity, new PlayerComponent { Name = "SYSTEM", Score = 0 });
        
        var match = new Match(matchId, state, loop, dispatcher, scheduler);
        
        // Hook into player join to spawn them
        match.OnPlayerJoined += (playerId) =>
        {
            Console.WriteLine($"[Match] Scheduling spawn for player {playerId}");
            var action = new SpawnPlayerAction { PlayerName = $"P-{playerId.ToString().Substring(0, 4)}" };
            
            // Schedule via GameLoop helper (wraps scheduler)
            // We schedule it slightly in the future to ensure it's picked up
            loop.ScheduleOnTick(loop.CurrentTick + 1, action, systemEntity);
        };
        
        return match;
    }
}
