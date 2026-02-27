# Part 2: Reactions

Reactions let you respond to actions automatically, enabling event-driven game logic without tight coupling.

## Creating Reactions

Reactions inherit from `ReactionService<TAction, TComponent>`:

```csharp
[NetworkId(2)]
public class DecreaseDamageReaction : ReactionService<DamageAction, HealthComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true; // Run after action
    
    protected override IsAborted React(DamageAction action, ref HealthComponent health, Context ctx)
    {
        // Modify action or component state
        // Return IsAborted { Value = true } to cancel the action
        return new IsAborted { Value = false };
    }
}
```

**Key Properties:**
- `Priority` - Execution order (higher runs first)
- `AfterActionExecuted` - `true` runs after action, `false` runs before
- `React()` - Returns `IsAborted` to optionally cancel the action

## Registering Reactions

Register reactions with the action service:

```csharp
var reactions = new[] { new DecreaseDamageReaction() };
dispatcher.RegisterAction<DamageAction, HealthComponent>(handler, reactions);
```

Or register globally:

```csharp
dispatcher.RegisterReaction(new RegionDamageReaction());
```

## Entity-Specific Reactions

Use tag components to attach reactions to specific entities:

```csharp
[NetworkId(200)]
public struct RegionDamageReactionTag : IComponent { }

// Add tag to entity
entity.AddReaction<RegionDamageReactionTag>(state);
```

## Hierarchy Reactions

Reactions bubble up parent hierarchies automatically:

```csharp
region.AddChild(player, state);
region.AddReaction<RegionDamageReactionTag>(state);

// Damage to player also triggers parent's reaction
gameLoop.Schedule(new DamageAction(10), player);
```

The reaction receives the parent entity in `ctx.Entity`, allowing access to parent components:

```csharp
protected override IsAborted React(DamageAction action, ref RegionDamageReactionTag tag, Context ctx)
{
    ref var region = ref ctx.GetComponent<RegionComponent>();
    region.DamageCounter += action.Amount;
    return new IsAborted { Value = false };
}
```

## Use Cases

Reactions enable many patterns:

- **Validation** - Modify or cap action parameters
- **Logging** - Track all actions for debugging or replay
- **Side effects** - Trigger achievements, spawn enemies, update UI
- **Cascading actions** - One action triggers others
- **Statistics** - Count actions, track metrics
- **Game rules** - Enforce complex constraints without modifying actions

## Execution Order

For a single action execution:

1. All `Before` reactions run in registration order
2. The action service executes
3. All `After` reactions run in registration order

This predictable order ensures deterministic behavior.

## Next Steps

- [Part 3: Rollback Networking](03-rollback.md) - Save and restore game state

See `PoCTest.cs` for complete code demonstrating reactions and hierarchy.
