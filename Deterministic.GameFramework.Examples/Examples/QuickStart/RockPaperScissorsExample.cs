using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples.QuickStart;

public static class RockPaperScissorsExample
{
    public static void Run()
    {
        Console.WriteLine("=== Rock Paper Scissors ===\n");

        // Create game state
        var game = new RockPaperScissorsGame();

        // Create game loop
        var gameLoop = new GameLoop();
        gameLoop.AddDomain(game);

        // Simulate a game
        SimulateGame(game, gameLoop);

        Console.WriteLine("\nGame Over!");
    }

    private static void SimulateGame(RockPaperScissorsGame game, GameLoop gameLoop)
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
            gameLoop.ExecuteAction(action1);
            
            // Small delay for readability
            Thread.Sleep(500);
            
            // Player 2 makes choice
            var p2Choice = (Choice)random.Next(3);
            var action2 = new MakeChoiceAction 
            { 
                PlayerName = game.Player2.Name,
                Choice = p2Choice
            };
            gameLoop.ExecuteAction(action2);
            
            // Wait before next round
            if (game.Winner == null)
            {
                Thread.Sleep(1000);
                Console.WriteLine("--- Next Round ---\n");
            }
        }
    }
}
