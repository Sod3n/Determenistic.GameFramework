# Hello World - Your First DAR Project

This tutorial introduces the core concepts of the DAR (Domain-Action-Reaction) framework by building a simple counter game.

## Core Concepts

Every DAR project has three fundamental building blocks:

1. **GameState** - The root of your domain tree, holds all game entities
2. **Domain** - Game entities that hold state (players, items, counters, etc.)
3. **Action** - Operations that modify domain state in a controlled way

## Prerequisites

- .NET 8.0 SDK
- Reference to `TurnBasedPrototype.Server.Core`

## Step 1: Create Your GameState

The `GameState` is the root of your domain tree. Every game needs exactly one.

```csharp
public class MyGameState : BaseGameState
{
    public MyGameState(Guid matchId) : base(matchId, randomSeed: 0) { }
}
```

Inherit from `BaseGameState` and pass a `matchId` (identifies the game session) and `randomSeed` (for deterministic randomness, covered in advanced topics).

## Step 2: Create a Domain

Domains are your game entities. They hold state and automatically register themselves in the domain tree when constructed.

```csharp
public class CounterDomain : LeafDomain
{
    public int Value { get; private set; } = 0;
    public CounterDomain(BranchDomain parent) : base(parent) { }
}
```

Use `LeafDomain` for entities without children, or `BranchDomain` for containers that hold other domains.

## Step 3: Create an Action

Actions modify game state in a controlled, observable way.

```csharp
public class IncrementAction : DARAction<CounterDomain, IncrementAction>
{
    public int Amount { get; set; } = 1;
    
    protected override void ExecuteProcess(CounterDomain counter)
    {
        counter.Increment(Amount);
    }
}
```

The generic parameters are `<TDomain, TSelf>` - the target domain type and the action type itself. Override `ExecuteProcess` to implement your logic.

## Step 4: Put It All Together

```csharp
var gameState = new MyGameState(Guid.NewGuid());
var counter = new CounterDomain(gameState);

new IncrementAction { Amount = 10 }.Execute(counter);
```

The counter automatically registers itself as a child of the game state. Actions are executed by calling `.Execute()` with the target domain.

## What Just Happened?

1. **GameState created** - Root of the domain tree
2. **CounterDomain added** - Automatically registered as a child
3. **Action executed** - Modified the counter's state through the action system

This pattern ensures all state changes are:
- **Observable** - Other systems can react to actions
- **Validated** - Actions can be checked before execution
- **Traceable** - All mutations go through the action system

## Next Steps

- [Part 2: Adding Reactions](02-reactions.md) — Respond to actions automatically
- [Part 3: Domain Hierarchy](03-domain-hierarchy.md) — Build complex entity trees
- [Part 4: Observable Attributes](04-observable-attributes.md) — Reactive UI bindings

See **Example 1** for the complete, runnable code.
