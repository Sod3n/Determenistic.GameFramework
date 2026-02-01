using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples.QuickStart;

public static class RockPaperScissorsExample
{
    public static void Run()
    {
        Console.WriteLine("=== Rock Paper Scissors ===\n");

        // Create game state
        var game = new RockPaperScissorsGame();

        // Simulate a game
        SimulateGame(game);

        Console.WriteLine("\nGame Over!");
    }

    private static void SimulateGame(RockPaperScissorsGame game)
    {
        var random = new Random(42); // Fixed seed for determinism
        
        while (game.Winner == null)
        {
            // Player 1 makes choice
            var p1Choice = (Choice)random.Next(3);
            var action1 = new MakeChoiceAction 
            { 
                PlayerName = game.Player1.Name,
                Choice = p1Choice
            };
            action1.Execute(game);
            
            // Small delay for readability
            Thread.Sleep(500);
            
            // Player 2 makes choice
            var p2Choice = (Choice)random.Next(3);
            var action2 = new MakeChoiceAction 
            { 
                PlayerName = game.Player2.Name,
                Choice = p2Choice
            };
            action2.Execute(game);
            
            // Wait before next round
            if (game.Winner == null)
            {
                Thread.Sleep(1000);
                Console.WriteLine("--- Next Round ---\n");
            }
        }
    }
}
