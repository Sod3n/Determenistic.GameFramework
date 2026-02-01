using Newtonsoft.Json;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Server.CollectiveActions;

namespace Deterministic.GameFramework.Examples.Network;

/// <summary>
/// Example 7: Collective Actions
/// - WaitForAll: Execute when all players submit
/// - VoteFor: Execute winning option after all vote
/// </summary>
public static class Example07_CollectiveActions
{
    public static void Run()
    {
        Console.WriteLine("=== Collective Actions Example ===\n");
        
        var game = new LobbyGameState();
        var player1 = new Player(game, Guid.NewGuid(), "Alice");
        var player2 = new Player(game, Guid.NewGuid(), "Bob");
        var player3 = new Player(game, Guid.NewGuid(), "Charlie");
        
        Console.WriteLine("--- WaitForAll: Ready Check ---");
        
        // Each player clicks "Ready"
        new ReadyAction().Execute(player1);
        new ReadyAction().Execute(player2);
        new ReadyAction().Execute(player3); // Triggers ExecuteWhenReady
        
        Console.WriteLine("\n--- VoteFor: Choose Difficulty ---");
        
        // Players vote for difficulty
        new VoteDifficultyAction { Difficulty = "Hard" }.Execute(player1);
        new VoteDifficultyAction { Difficulty = "Normal" }.Execute(player2);
        new VoteDifficultyAction { Difficulty = "Hard" }.Execute(player3); // Hard wins
        
        game.Dispose();
    }
}

// --- Game State ---

public interface IRequireLobbyGameState : IDARAction
{
    LobbyGameState? Game { get; set; }
}

public class LobbyGameState : BranchDomain
{
    public CollectiveActionManager CollectiveActions { get; }
    public bool GameStarted { get; set; }
    public string? SelectedDifficulty { get; set; }
    
    public LobbyGameState() : base(null)
    {
        CollectiveActions = new CollectiveActionManager(this, () => 
            GetAll<Player>().Select(p => p.PlayerId));
        
        new Reaction<LeafDomain, IRequireLobbyGameState>(this)
            .Prepare((_, action) => action.Game = this)
            .AddTo(Disposables);
    }
}

public class Player : BranchDomain
{
    public Guid PlayerId { get; }
    public string Name { get; }
    
    public Player(BranchDomain parent, Guid playerId, string name) : base(parent)
    {
        PlayerId = playerId;
        Name = name;
        
        // Actually should be placed on Client Domain and send through network
        new Reaction<LeafDomain, INetworkAction>(this)
            .Prepare((_, action) => action.ExecutorId ??= PlayerId)
            .AddTo(Disposables);
    }
}

// --- WaitForAll Action ---

public class ReadyAction : WaitForAllAction<Player, ReadyAction>, IRequireLobbyGameState
{
    [JsonIgnore] public LobbyGameState? Game { get; set; }
    
    public override string CollectiveKey => "LobbyReady";
    
    protected override void ExecuteWhenReady(Player player)
    {
        Game!.GameStarted = true;
        Console.WriteLine("All players ready! Starting game...");
    }
    
    protected override void OnPlayerSubmitted(Player player)
    {
        var (submitted, total) = Game!.CollectiveActions.GetWaitForAllStatus(CollectiveKey);
        Console.WriteLine($"{player.Name} is ready ({submitted}/{total})");
    }
}

// --- VoteFor Action ---

public class VoteDifficultyAction : VoteForAction<Player, VoteDifficultyAction>, IRequireLobbyGameState
{
    [JsonIgnore] public LobbyGameState? Game { get; set; }
    
    public override string CollectiveKey => "DifficultyVote";
    public override string VoteOption => Difficulty;
    
    public string Difficulty { get; set; } = "Normal";
    
    protected override void ExecuteWhenWon(Player player)
    {
        Game!.SelectedDifficulty = Difficulty;
        Console.WriteLine($"Difficulty selected: {Difficulty} (votes: {string.Join(", ", FinalVoteCounts!.Select(kv => $"{kv.Key}={kv.Value}"))})");
    }
    
    protected override void OnVoteSubmitted(Player player)
    {
        Console.WriteLine($"{player.Name} voted for {Difficulty}");
    }
}
