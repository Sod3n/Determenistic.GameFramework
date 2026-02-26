# Quick Start Guide

This guide will walk you through setting up a simple "Game Loop" where a player moves and takes damage.

## 1. Define Components

Create your data structs. Remember to add the `[NetworkId]` attribute!

```csharp
using Deterministic.GameFramework.CoreV2;

[NetworkId(100)]
public struct Transform : IComponent
{
    public Vector2 Position;
}

[NetworkId(101)]
public struct PlayerStats : IComponent
{
    public Int Health;
}
```

## 2. Define Actions

Create actions to modify the state.

```csharp
public struct MovePlayer : IAction
{
    public Vector2 Delta;
}

public struct TakeDamage : IAction
{
    public int Amount;
}
```

## 3. Register Systems

Create a `Dispatcher` and register handlers for your actions.

```csharp
var dispatcher = new Dispatcher();

dispatcher.Register<MovePlayer>((action, state, entity) => 
{
    if (state.HasComponent<Transform>(entity))
    {
        ref var transform = ref state.GetState<Transform>(entity);
        transform.Position += action.Delta;
        Console.WriteLine($"Player moved to {transform.Position}");
    }
});

dispatcher.Register<TakeDamage>((action, state, entity) =>
{
    if (state.HasComponent<PlayerStats>(entity))
    {
        ref var stats = ref state.GetState<PlayerStats>(entity);
        stats.Health -= action.Amount;
        Console.WriteLine($"Player took {action.Amount} damage. Health: {stats.Health}");
    }
});
```

## 4. Setup the Game World

Initialize the `GlobalState` and create an entity.

```csharp
var state = new GlobalState();

// Register components
state.RegisterComponent<Transform>();
state.RegisterComponent<PlayerStats>();

// Create Player
var player = state.CreateEntity();
state.AddComponent(player, new Transform { Position = Vector2.Zero });
state.AddComponent(player, new PlayerStats { Health = 100 });
```

## 5. Run the Loop

Execute actions to simulate gameplay.

```csharp
// Simulate a frame
Console.WriteLine("--- Tick 1 ---");
state.Execute(new MovePlayer { Delta = new Vector2(1, 0) }, player, dispatcher);
state.Execute(new TakeDamage { Amount = 10 }, player, dispatcher);

// Simulate another frame
Console.WriteLine("--- Tick 2 ---");
state.Execute(new MovePlayer { Delta = new Vector2(0, 1) }, player, dispatcher);
```

## 6. Save & Load (Optional)

You can serialize the entire state to bytes and restore it later.

```csharp
// Save
byte[] snapshot = StateSerializer.Serialize(state);

// Load into a new state
var newState = new GlobalState();
StateSerializer.Deserialize(newState, snapshot);
```
