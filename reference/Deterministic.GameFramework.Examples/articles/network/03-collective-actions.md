# Part 6: Collective Actions

Coordinate actions that require multiple players to participate.

## What You'll Learn

- WaitForAll: Actions that execute when all players submit
- VoteFor: Actions where players vote and majority wins
- Setting up CollectiveActionManager

## Use Cases

| Pattern | Use Case |
|---------|----------|
| WaitForAll | "Ready" buttons, simultaneous reveals, turn confirmation |
| VoteFor | Path selection, difficulty voting, map choice |

## Setup

Add `CollectiveActionManager` to your game state:

```csharp
public class MyGameState : BranchDomain
{
    public CollectiveActionManager CollectiveActions { get; }
    
    public MyGameState() : base(null)
    {
        CollectiveActions = new CollectiveActionManager(this, GetPlayerIds);
    }
    
    private IEnumerable<Guid> GetPlayerIds() => 
        GetAll<PlayerDomain>().Select(p => p.PlayerId);
}
```

## WaitForAll Actions

Execute logic only after all players have submitted:

```csharp
public class ReadyAction : WaitForAllAction<MyGameState, ReadyAction>
{
    public override string CollectiveKey => "BattleReady";
    
    protected override void ExecuteWhenReady(MyGameState game)
    {
        // Runs once when ALL players have submitted
        game.StartBattle();
    }
    
    protected override void OnPlayerSubmitted(MyGameState game)
    {
        // Runs for each player's submission (optional)
        var (submitted, total) = game.CollectiveActions.GetWaitForAllStatus(CollectiveKey);
        Console.WriteLine($"Ready: {submitted}/{total}");
    }
}
```

## VoteFor Actions

Let players vote, execute the winning option:

```csharp
public class VotePathAction : VoteForAction<MyGameState, VotePathAction>
{
    public override string CollectiveKey => "ChoosePath";
    public override string VoteOption { get; set; } // "Forest", "Cave", etc.
    
    protected override void ExecuteWhenWon(MyGameState game)
    {
        // Runs only for the winning option
        game.SetPath(VoteOption);
        Console.WriteLine($"Path chosen: {VoteOption} with {FinalVoteCounts[VoteOption]} votes");
    }
}
```

## Flow Diagram

```
WaitForAll:
  Player A submits ──► Waiting...
  Player B submits ──► Waiting...
  Player C submits ──► All ready! ──► ExecuteWhenReady()

VoteFor:
  Player A votes "Forest" ──► Waiting...
  Player B votes "Cave"   ──► Waiting...
  Player C votes "Forest" ──► All voted! ──► "Forest" wins ──► ExecuteWhenWon()
```

## Key Properties

| Property | Description |
|----------|-------------|
| `CollectiveKey` | Groups related actions together |
| `VoteOption` | The choice this vote represents |
| `IsCollectiveExecution` | True when triggered by all players ready |
| `IsWinningExecution` | True when this option won the vote |
| `FinalVoteCounts` | Vote tallies (VoteFor only) |

## Query Methods

```csharp
// Check submission status
var (submitted, total) = manager.GetWaitForAllStatus("BattleReady");
bool hasSubmitted = manager.HasSubmitted("BattleReady", playerId);

// Check vote status
var counts = manager.GetVoteCounts("ChoosePath"); // {"Forest": 2, "Cave": 1}
bool hasVoted = manager.HasVoted("ChoosePath", playerId);

// Cancel pending action (timeout, disconnect)
manager.CancelCollectiveAction("BattleReady");
```

## Events

```csharp
manager.OnWaitForAllReady += action => Console.WriteLine("All players ready!");
manager.OnVoteResolved += (action, counts) => Console.WriteLine($"Vote resolved: {action.VoteOption}");
```
