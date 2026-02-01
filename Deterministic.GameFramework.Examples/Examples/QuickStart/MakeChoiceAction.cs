using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples.QuickStart;

/// <summary>
/// Action: Player makes a choice
/// </summary>
public class MakeChoiceAction : DARAction<RockPaperScissorsGame>
{
    public string PlayerName { get; set; } = "";
    public Choice Choice { get; set; }

    public override void Execute(RockPaperScissorsGame domain)
    {
        // Find the player
        var player = domain.Player1.Name == PlayerName ? domain.Player1 : domain.Player2;
        
        // Set their choice
        player.CurrentChoice = Choice;
        
        Console.WriteLine($"{PlayerName} chose {Choice}");
        
        // Check if both players have chosen
        if (domain.Player1.CurrentChoice.HasValue && domain.Player2.CurrentChoice.HasValue)
        {
            // Resolve the round
            ResolveRound(domain);
        }
    }

    private void ResolveRound(RockPaperScissorsGame game)
    {
        var p1Choice = game.Player1.CurrentChoice!.Value;
        var p2Choice = game.Player2.CurrentChoice!.Value;

        Console.WriteLine($"\nRound {game.RoundNumber}:");
        Console.WriteLine($"  {game.Player1.Name}: {p1Choice}");
        Console.WriteLine($"  {game.Player2.Name}: {p2Choice}");

        // Determine winner
        if (p1Choice == p2Choice)
        {
            Console.WriteLine("  Result: Draw!");
        }
        else if (IsWinner(p1Choice, p2Choice))
        {
            game.Player1.Score++;
            Console.WriteLine($"  Winner: {game.Player1.Name}!");
        }
        else
        {
            game.Player2.Score++;
            Console.WriteLine($"  Winner: {game.Player2.Name}!");
        }

        Console.WriteLine($"\nScore: {game.Player1.Name} {game.Player1.Score} - {game.Player2.Score} {game.Player2.Name}\n");

        // Check for game winner (best of 3)
        if (game.Player1.Score >= 2)
        {
            game.Winner = game.Player1.Name;
            Console.WriteLine($"🏆 {game.Player1.Name} wins the game!");
        }
        else if (game.Player2.Score >= 2)
        {
            game.Winner = game.Player2.Name;
            Console.WriteLine($"🏆 {game.Player2.Name} wins the game!");
        }
        else
        {
            // Next round
            game.RoundNumber++;
            game.Player1.CurrentChoice = null;
            game.Player2.CurrentChoice = null;
        }
    }

    private bool IsWinner(Choice a, Choice b)
    {
        return (a == Choice.Rock && b == Choice.Scissors) ||
               (a == Choice.Paper && b == Choice.Rock) ||
               (a == Choice.Scissors && b == Choice.Paper);
    }
}
