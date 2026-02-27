using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Client;
using Shared;

Console.WriteLine("=== V2 Multiplayer Example Client ===");

// 1. Setup Core V2 components
var state = new GlobalState();

// IMPORTANT: Register all component types that will be deserialized
// This ensures the StateSerializer knows about them
state.RegisterComponent<PlayerComponent>();

var dispatcher = new Dispatcher();
dispatcher.RegisterAction(new IncrementScoreService(), Array.Empty<ReactionService<IncrementScoreAction, PlayerComponent>>());

var scheduler = new ActionScheduler();
var gameLoop = new GameLoop(state, dispatcher, scheduler);

// 2. Setup Network V2 Client
var serverUrl = "http://localhost:5005/gamehub";
var matchId = Guid.Parse("00000000-0000-0000-0000-000000000001");

await using var client = new GameClient(serverUrl, state, dispatcher, scheduler, gameLoop);

// Simple logging
client.OnLog += Console.WriteLine;

try 
{
    // 3. Connect and Start Loop
    await client.ConnectAsync(matchId);
    
    Console.WriteLine("Waiting for server state...");
    await client.WaitForSyncAsync();
    Console.WriteLine($"Synced! Starting at Tick {gameLoop.CurrentTick}");
    
    Console.WriteLine("Starting game loop...");
    _ = gameLoop.Start();
    Console.WriteLine("Game loop started!");

    Console.WriteLine("Connected! Press 'S' to increment score, 'Q' to quit.");

    while (true)
    {
        var key = Console.ReadKey(true).Key;
        if (key == ConsoleKey.Q) break;
        if (key == ConsoleKey.S)
        {
            // Execute Action (Client-side prediction enabled by default)
            var action = new IncrementScoreAction { Amount = 1 };
            client.Execute(action, 0); // Target Entity 0, default tick delay 5
            
            Console.WriteLine($"Action Sent! (Amount: {action.Amount})");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
