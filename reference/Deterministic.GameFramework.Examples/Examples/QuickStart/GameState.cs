using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.QuickStart;

/// <summary>
/// Root game state - contains all game data
/// </summary>
public class RockPaperScissorsGame : BranchDomain
{
    public PlayerDomain Player1 { get; }
    public PlayerDomain Player2 { get; }
    public int RoundNumber { get; set; } = 1;
    public string? Winner { get; set; }

    public RockPaperScissorsGame() : base(null)
    {
        Player1 = new PlayerDomain(this, "Player 1");
        Player2 = new PlayerDomain(this, "Player 2");
    }
}

/// <summary>
/// Player domain - tracks player state
/// </summary>
public class PlayerDomain : BranchDomain
{
    public string Name { get; }
    public Choice? CurrentChoice { get; set; }
    public int Score { get; set; }

    public PlayerDomain(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}

/// <summary>
/// Player choices
/// </summary>
public enum Choice
{
    Rock,
    Paper,
    Scissors
}
