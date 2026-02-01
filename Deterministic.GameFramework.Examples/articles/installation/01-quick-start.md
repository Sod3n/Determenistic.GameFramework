# Quick Start: Your First Deterministic Game

This tutorial guides you through creating a simple Rock-Paper-Scissors game using the Deterministic Game Framework. You'll learn the core concepts by building a working multiplayer game in about 15 minutes.

## What We'll Build

A **Rock-Paper-Scissors** game featuring:
- Deterministic game state management
- Player actions and turn resolution
- Win condition checking
- Foundation for network synchronization

**Time to complete:** ~15 minutes  
**Full example code:** `Examples/QuickStart/` folder

## Prerequisites

- Framework installed ([Installation Guide](00-installation-setup.md))
- Basic C# knowledge
- .NET 8.0 SDK

## Step 1: Create Your Game Project

Create a new console project and add framework references:

```bash
dotnet new console -n RockPaperScissors.Server
cd RockPaperScissors.Server
```

Add these project references to your `.csproj`:
```xml
<ItemGroup>
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Core\Deterministic.GameFramework.Core.csproj" />
  <ProjectReference Include="..\Framework\Deterministic.GameFramework.Server\Deterministic.GameFramework.Server.csproj" />
</ItemGroup>
```

## Step 2: Define Game State

The game state represents all data in your game. Create three components:

**1. Root Game State** - Contains players and game status  
**2. Player Domain** - Tracks individual player data  
**3. Choice Enum** - Defines valid player choices

**Key concepts:**
- Inherit from `BranchDomain` to create domains that can have children
- Use constructor to initialize child domains
- All game data must be in domain properties

**See the complete implementation:** `Examples/QuickStart/GameState.cs`

The game state includes:
- Two player domains (Player1, Player2)
- Round tracking
- Winner determination
- Score management

## Step 3: Create Game Actions

Actions modify the game state. Our `MakeChoiceAction` handles:

**Action flow:**
1. Player selects Rock, Paper, or Scissors
2. Action stores the choice
3. When both players choose, resolve the round
4. Update scores and check for winner

**Key action concepts:**
- Inherit from `DARAction<TGameState>`
- Implement `Execute()` method
- Actions must be deterministic (same input = same output)
- Keep all logic inside the action

**See the complete implementation:** `Examples/QuickStart/MakeChoiceAction.cs`

The action includes:
- Player choice validation
- Round resolution logic
- Score updates
- Win condition checking

## Step 4: Run the Game

The game loop manages action execution and ensures deterministic processing.

**Core game loop pattern:**
```csharp
var game = new GameState();
var gameLoop = new GameLoop();
gameLoop.AddDomain(game);
gameLoop.ExecuteAction(action);
```

**See the complete implementation:** `Examples/QuickStart/RockPaperScissorsExample.cs`

## Step 5: Build and Test

```bash
dotnet build
dotnet run
```

**Expected behavior:**
- Players make random choices (using fixed seed for determinism)
- Rounds resolve automatically
- Scores update after each round
- Game ends when a player reaches 2 wins
- Same seed always produces same results

## Understanding the Architecture

### Domain Hierarchy
```
GameState (BranchDomain)
├── Player1 (PlayerDomain)
└── Player2 (PlayerDomain)
```

Domains organize your game state into logical components. Parent domains contain child domains, creating a tree structure.

### Action Processing
1. Create action with data
2. Pass to GameLoop
3. GameLoop executes action
4. Action modifies domain
5. Reactions fire (if any)

### Determinism
The framework ensures:
- Actions execute in order
- Same inputs produce same outputs
- Random numbers use seeded generators
- No external dependencies in game logic

## Key Concepts Demonstrated

✅ **Domain Hierarchy** - Organizing state into parent-child relationships  
✅ **Action Pattern** - Encapsulating state changes in actions  
✅ **Game Loop** - Managing deterministic execution  
✅ **State Encapsulation** - All game data lives in domains  

## Common Patterns

### Validation Pattern
Check conditions before executing logic:
- Validate game state isn't finished
- Verify player exists
- Ensure valid choices

### Query Pattern
Find domains in the hierarchy:
- `GetAll<T>()` - Find all domains of type T
- `GetFirst<T>()` - Find first domain of type T
- `GetInParent<T>()` - Navigate up the tree

### Reaction Pattern
Respond to actions without modifying action code:
- Listen for specific actions
- Execute side effects
- Update UI or logs

## Next Steps

### Add Multiplayer
Transform this into a networked game:
- **[Network Setup](../network/00-hello-world-multiplayer.md)** - Add SignalR server
- **[Network Actions](../getting-started/05-network-actions.md)** - Synchronize between clients

### Enhance Game Logic
Expand your understanding:
- **[Reactions](../getting-started/02-reactions.md)** - Add event handling
- **[Domain Hierarchy](../getting-started/03-domain-hierarchy.md)** - Complex state organization
- **[Observable Attributes](../getting-started/04-observable-attributes.md)** - Reactive UI bindings

### Add Features
Practice what you learned:
- Best of 5 rounds instead of 3
- Add player names from input
- Implement draw handling
- Add match history tracking

## Troubleshooting

**Actions not executing?**  
Use `gameLoop.ExecuteAction()`, not `action.Execute()` directly. The GameLoop handles proper processing.

**State not updating?**  
Verify your action's `Execute()` method modifies the domain properties. Check that domain references are correct.

**Non-deterministic behavior?**  
- Use fixed random seeds: `new Random(42)`
- Avoid `DateTime.Now` - use framework time
- Don't iterate `Dictionary` directly - use `OrderBy()` first

## Complete Example

The full working code is in:
- `Examples/QuickStart/GameState.cs`
- `Examples/QuickStart/MakeChoiceAction.cs`
- `Examples/QuickStart/RockPaperScissorsExample.cs`

Run it from the Examples project:
```bash
cd Framework/Deterministic.GameFramework.Examples
dotnet run -- quickstart
```

## What You've Learned

You now understand:
- How to structure game state with domains
- How to modify state with actions
- How the game loop ensures determinism
- The foundation for multiplayer games

Continue with the Network Setup guide to add multiplayer support, or explore the Getting Started section to deepen your understanding of the framework's capabilities.
