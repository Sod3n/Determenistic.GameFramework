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

## 5. Hierarchy Reactions (Bubbling)

The framework supports **Hierarchy Bubbling** for reactions. This means an action executed on a child entity can trigger reactions on its parent (and ancestors).

This is useful for systems like:
- **Region/Dungeon tracking**: A region tracks total damage taken by all monsters within it.
- **Quest Objectives**: "Kill 10 skeletons" objective on a player updates when a skeleton child entity dies.

### Tag Component Pattern

To attach a reaction to an ancestor entity without polluting it with unrelated data components, we use the **Tag Component Pattern**.

1. **Define a Tag Component**: This component acts as a subscription marker.
```csharp
[NetworkId(107)]
public struct RegionDamageReactionTag : IComponent { }
```

2. **Define the Reaction**: Use the Tag as the `TTarget`.
```csharp
public class RegionDamageReaction : ReactionService<DamageAction, RegionDamageReactionTag>
{
    protected override bool React(DamageAction action, ref RegionDamageReactionTag tag, Context ctx)
    {
        // ctx.Entity is the Ancestor (The Region)
        ref var region = ref ctx.GetComponent<RegionComponent>();
        region.TotalDamage += action.Amount;
        return false;
    }
}
```

3. **Setup the Hierarchy**:
```csharp
// Parent (Region) has the Tag
state.AddComponent(regionEntity, new RegionComponent());

// Subscribe to reaction using helper
regionEntity.AddReaction<RegionDamageReactionTag>(state);

// Child (Player/Monster)
regionEntity.AddChild(monsterEntity, state);
```

When `DamageAction` runs on `monsterEntity`, the dispatcher will:
1. Run local reactions on `monsterEntity`.
2. Bubble up to `regionEntity`.
3. Find `RegionDamageReactionTag` on `regionEntity`.
4. Execute `RegionDamageReaction` with `ctx.Entity` set to `regionEntity`.

## Summary

| Component | Responsibility |
|---|---|
| **Action** | Data payload (Parameters) |
| **ActionService** | The "main" logic (Mutation) |
| **Pre-Reaction** | Validation, Interception, Cancellation |
| **Post-Reaction** | Analytics, UI events, Cascading game logic |
