# Hello World - Your First Deterministic Game

This tutorial introduces the core concepts of the framework by building a simple damage system.

## Core Concepts

Every game built with this framework has three fundamental building blocks:

1. **Components** - Data structs that hold state (health, position, etc.)
2. **Actions** - Operations that modify component state
3. **Services** - Stateless handlers that execute actions

## Components

Components are simple data structs with the `[NetworkId]` attribute:

```csharp
[NetworkId(100)]
public struct HealthComponent : IComponent
{
    public Int CurrentHealth;
}
```

Use deterministic types like `Int` instead of `int` for networked data.

## Actions

Actions describe state changes and carry data:

```csharp
[NetworkId(1)]
public struct DamageAction : IAction
{
    public Int Amount;
}
```

## Action Services

Services execute actions. They're stateless and operate on components:

```csharp
public class DamageActionHandler : ActionService<DamageAction, HealthComponent>
{
    public override void Execute(DamageAction action, ref HealthComponent health, 
                                 GlobalState state, Entity entity)
    {
        health.CurrentHealth -= action.Amount;
    }
}
```

## Putting It Together

```csharp
var state = new GlobalState();
var dispatcher = new Dispatcher(/* ... */);
var gameLoop = new GameLoop(state, dispatcher, scheduler);

dispatcher.RegisterAction<DamageAction, HealthComponent>(new DamageActionHandler());

var player = new Entity(1);
state.AddComponent(player, new HealthComponent { CurrentHealth = 100 });

gameLoop.Schedule(new DamageAction(15), player);
gameLoop.RunSingleTick();
```

## What Just Happened?

1. **Component created** - Holds the player's health data
2. **Action scheduled** - Queued for execution on the next tick
3. **Service executed** - Modified the component state
4. **State updated** - Health reduced from 100 to 85

This pattern ensures all state changes are:
- **Deterministic** - Same inputs produce same outputs
- **Observable** - Other systems can react to actions
- **Validated** - Actions can be checked before execution
- **Networked** - Actions serialize and sync across clients

## Next Steps

- [Part 2: Reactions](02-reactions.md) - Respond to actions automatically
- [Part 3: Rollback Networking](03-rollback.md) - Save and restore game state

See `PoCTest.cs` for the complete, runnable code.
