using MultiplayerHelloWorld.Shared;
using Deterministic.GameFramework.Server;

Console.WriteLine("=== Multiplayer Counter Client ===");
Console.WriteLine();

// Get client name
Console.Write("Enter your name: ");
var clientName = Console.ReadLine() ?? "Player";

var userId = Guid.NewGuid();
var matchId = Guid.Parse("00000000-0000-0000-0000-000000000001");

// Create client and connect to server (one line handles all SignalR setup)
var client = new ClientDomain(userId, matchId);
var connection = await client.ConnectToServer("http://localhost:5000/gamehub");

try
{
    Console.WriteLine($"[{clientName}] Connected to server");
    Console.WriteLine($"[{clientName}] Joined match {matchId}");
    Console.WriteLine();
    ShowHelp();
    
    // Main loop
    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(input))
            continue;
            
        var userInput = ParseInput(input);
        
        switch (userInput.Type)
        {
            case InputCommand.Quit:
                return;
                
            case InputCommand.Increment:
                client.Send(new IncrementAction { Amount = userInput.Amount });
                Console.WriteLine($"[{clientName}] Sent +{userInput.Amount}");
                break;
                
            case InputCommand.Help:
                ShowHelp();
                break;
                
            case InputCommand.Invalid:
                Console.WriteLine($"Invalid command: '{input}'. Type 'help' for commands.");
                break;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine("Make sure the server is running on http://localhost:5000");
}
finally
{
    await connection.StopAsync();
    Console.WriteLine($"[{clientName}] Disconnected");
}

return;

// Helper functions and types
static UserInput ParseInput(string input)
{
    return input.ToLower() switch
    {
        "q" or "quit" => new UserInput(InputCommand.Quit),
        "h" or "help" or "?" => new UserInput(InputCommand.Help),
        var s when s.StartsWith("+") && int.TryParse(s[1..], out var amount) && amount > 0 
            => new UserInput(InputCommand.Increment, amount),
        _ => new UserInput(InputCommand.Invalid)
    };
}

static void ShowHelp()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  +N     Increment counter by N (e.g., +5, +10)");
    Console.WriteLine("  help   Show this help message");
    Console.WriteLine("  quit   Disconnect and exit");
    Console.WriteLine();
}

enum InputCommand { Increment, Quit, Help, Invalid }
record UserInput(InputCommand Type, int Amount = 0);
