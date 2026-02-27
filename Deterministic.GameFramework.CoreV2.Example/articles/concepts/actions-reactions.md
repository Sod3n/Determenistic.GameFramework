# Actions & Reactions

The **Action-Reaction** pattern is the primary way logic is structured in the framework.

- **Actions** describe *what* happens (Intent).
- **Services** describe *how* it happens (Execution).
- **Reactions** describe *side effects* (Consequences).

## 1. Defining an Action

An action is a pure data struct that implements `IAction`.

```csharp
[NetworkId(200)]
public struct HealAction : IAction
{
    public Int Amount;
}
```

## 2. Handling an Action (ActionService)

To execute an action, you implement an `ActionService<TAction, TTarget>`.
This service handles the core logic of the action.

```csharp
public class HealService : ActionService<HealAction, HealthComponent>
{
    protected override void Execute(HealAction action, ref HealthComponent health, Context context)
    {
        health.Current += action.Amount;
        
        // Clamp to max
        if (health.Current > health.Max) 
            health.Current = health.Max;
            
        Console.WriteLine($"Healed {action.Amount}. New Health: {health.Current}");
    }
}
```

## 3. Reactions

Reactions allow other parts of the system to respond to an action **without modifying the original service**. This decouples your code.

For example, maybe when a player is healed, we want to play a sound, or show a particle effect, or trigger a "Heal Over Time" buff removal.

### Creating a Reaction

Implement `ReactionService<TAction, TTarget>`.

```csharp
public class OnHealLogReaction : ReactionService<HealAction, HealthComponent>
{
    protected override void React(HealAction action, ref HealthComponent health, Context context)
    {
        // This runs AFTER the heal has been applied
        if (health.Current == health.Max)
        {
            Console.WriteLine("Target is fully healed!");
        }
    }
}
```

### Reaction Types

You can control *when* a reaction runs relative to the main action execution.

- **Pre-Reaction** (`AfterActionExecuted = false`): Runs *before* the action modifies the state. Can cancel the action!
- **Post-Reaction** (`AfterActionExecuted = true`): Runs *after* the action. Good for side effects.

```csharp
public class PreventOverhealReaction : ReactionService<HealAction, HealthComponent>
{
    public PreventOverhealReaction()
    {
        AfterActionExecuted = false; // Run BEFORE
        Priority = 100; // Run early
    }

    protected override bool React(HealAction action, ref HealthComponent health, Context context)
    {
        if (health.Current >= health.Max)
        {
            Console.WriteLine("Heal prevented: Already at full health.");
            return true; // ABORT the action!
        }
        return false;
    }
}
```

## 4. Registration

You register services and reactions with the `Dispatcher`.

```csharp
// Manual registration (normally handled by DI or startup logic)
var healService = new HealService();
var reactions = new List<ReactionService<HealAction, HealthComponent>> 
{ 
    new OnHealLogReaction(),
    new PreventOverhealReaction() 
};

dispatcher.RegisterAction(healService, reactions);
```

## 5. Conditional Reactions & Tag Pattern

Sometimes you want a reaction to run only under specific conditions, or you want to attach a reaction to an entity without adding data to it.

### The Tag Pattern

You can use a "Tag Component" as the `TTarget` for a reaction. This effectively "subscribes" the entity to the reaction.

1. **Define a Tag**:
```csharp
[NetworkId(107)]
public struct BurnEffectTag : IComponent 
{
    public int DamagePerTick;
}
```

2. **Define the Reaction**:
```csharp
public class BurnReaction : ReactionService<TickAction, BurnEffectTag>
{
    protected override IsAborted React(TickAction action, ref BurnEffectTag tag, Context ctx)
    {
        // Apply burn damage
        ref var health = ref ctx.GetComponent<HealthComponent>();
        health.Current -= tag.DamagePerTick;
        return new IsAborted { Value = false };
    }
}
```

3. **Subscribe**:
```csharp
// Attach the reaction to the entity
entity.AddReaction(state, new BurnEffectTag { DamagePerTick = 5 });
```

### Conditional Execution (`ShouldReact`)

You can override `ShouldReact` to add custom filtering logic before the reaction runs. This is useful for complex conditions that depend on multiple components.

```csharp
public class CriticalHitReaction : ReactionService<DamageAction, HealthComponent>
{
    protected override bool ShouldReact(DamageAction action, ref HealthComponent health, Context ctx)
    {
        // Only run if the entity has a "CriticalWeakness" component
        return ctx.Entity.HasComponent<CriticalWeakness>(ctx);
    }

    protected override IsAborted React(DamageAction action, ref HealthComponent health, Context ctx)
    {
        // Double the damage!
        // Note: This is a Pre-Reaction (AfterActionExecuted = false)
        // We are modifying the Action or State before the main handler? 
        // Actually, React args are by value usually, unless ref?
        // In this framework, Action is passed by value to React.
        // So you can't modify the Action here. You would modify state.
        
        Console.WriteLine("Critical Hit Logic!");
        return new IsAborted { Value = false };
    }
}
```

## Summary

| Component | Responsibility |
|---|---|
| **Action** | Data payload (Parameters) |
| **ActionService** | The "main" logic (Mutation) |
| **Pre-Reaction** | Validation, Interception, Cancellation |
| **Post-Reaction** | Analytics, UI events, Cascading game logic |
