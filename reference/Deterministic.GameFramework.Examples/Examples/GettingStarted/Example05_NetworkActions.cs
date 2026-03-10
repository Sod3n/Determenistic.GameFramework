using Newtonsoft.Json;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.GettingStarted;

/// <summary>
/// Example 5: Network Actions - Simulated client-server communication
/// </summary>
public static class Example05_NetworkActions
{
    public static void Run()
    {
        // Server has authoritative game state
        var server = new GameServer();
        
        // Clients have their own copies
        var clientA = new GameClient("Alice");
        var clientB = new GameClient("Bob");
        
        // Client A sends an action
        var actionJson = clientA.CreateMoveAction(x: 10, y: 20);
        Console.WriteLine($"Client A sends: {actionJson}");
        
        // Server receives, validates, executes, and broadcasts
        server.ReceiveAction(actionJson, broadcastTo: [clientA, clientB]);
        
        // Both clients now have the same state
        Console.WriteLine($"Server state: Player at ({server.PlayerX}, {server.PlayerY})");
        Console.WriteLine($"Client A state: Player at ({clientA.PlayerX}, {clientA.PlayerY})");
        Console.WriteLine($"Client B state: Player at ({clientB.PlayerX}, {clientB.PlayerY})");
    }
}

// --- Simulated Server ---

public class GameServer
{
    private readonly RootDomain _root = new();
    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    
    public void ReceiveAction(string json, GameClient[] broadcastTo)
    {
        var action = JsonConvert.DeserializeObject<MoveAction>(json)!;
        
        // Validate
        if (action.X < 0 || action.Y < 0)
        {
            Console.WriteLine("Server: Invalid action rejected");
            return;
        }
        
        // Execute on server
        PlayerX = action.X;
        PlayerY = action.Y;
        Console.WriteLine($"Server: Executed move to ({action.X}, {action.Y})");
        
        // Broadcast to all clients
        foreach (var client in broadcastTo)
            client.ReceiveAction(json);
    }
}

// --- Simulated Client ---

public class GameClient
{
    public string Name { get; }
    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    
    public GameClient(string name) => Name = name;
    
    public string CreateMoveAction(int x, int y)
    {
        return JsonConvert.SerializeObject(new MoveAction { X = x, Y = y });
    }
    
    public void ReceiveAction(string json)
    {
        var action = JsonConvert.DeserializeObject<MoveAction>(json)!;
        PlayerX = action.X;
        PlayerY = action.Y;
        Console.WriteLine($"Client {Name}: Received and applied move");
    }
}

// --- Network Action ---

public class MoveAction
{
    [JsonProperty] public int X { get; set; }
    [JsonProperty] public int Y { get; set; }
}

// Note: In production, use NetworkActionExecutor with BaseGameState
// for automatic domain lookup, validation, and history tracking.
