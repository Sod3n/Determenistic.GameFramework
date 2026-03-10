# Part 2: Adding Reactions

Reactions let you respond to actions automatically, enabling event-driven game logic without tight coupling.

## The DAR Pattern

This framework follows the **DAR (Domain-Action-Reaction)** pattern:

- **Domain** - Game entities that hold state
- **Action** - Operations that modify domains
- **Reaction** - Automatic responses to actions

Reactions observe the action system and execute logic at specific points in the action lifecycle. This enables validation, logging, side effects, and complex game mechanics without modifying action code.

## The Reaction Lifecycle

Reactions hook into four stages of action execution:

1. **Prepare** - Modify action parameters before validation
2. **Abort** - Cancel the action (return true to abort)
3. **Before** - Run logic before the action executes
4. **After** - Run logic after the action completes

This lifecycle enables powerful patterns like action modification, validation, and cascading effects.

## Creating Reactions

Reactions are created with `Reaction<TDomain, TAction>` and attached to a domain:

```csharp
new Reaction<CounterDomain, IncrementAction>(this)
    .After((counter, action) => Console.WriteLine($"Incremented by {action.Amount}"))
    .AddTo(Disposables);
```

The reaction observes all `IncrementAction` executions on `CounterDomain` instances in the domain tree. The `.AddTo(Disposables)` ensures proper cleanup when the parent domain is disposed.

## Validation with Abort

The `Abort` hook can prevent actions from executing:

```csharp
.Abort((counter, action) => action.Amount < 0)  // Return true to cancel
```

If any abort reaction returns `true`, the action is cancelled and `ExecuteProcess` never runs.

## Modifying Actions with Prepare

The `Prepare` hook can modify action parameters before validation:

```csharp
.Prepare((counter, action) => action.Amount *= 2)  // Double all increments
```

This runs before `Abort` checks, allowing you to normalize or adjust action data.

## Before and After Hooks

`Before` runs after validation but before the action executes. `After` runs after the action completes. Both are useful for side effects, logging, and triggering cascading actions.

## Chaining Hooks

A single reaction can have multiple hooks:

```csharp
new Reaction<CounterDomain, IncrementAction>(this)
    .Prepare((counter, action) => /* modify */)
    .Abort((counter, action) => /* validate */)
    .Before((counter, action) => /* pre-logic */)
    .After((counter, action) => /* post-logic */)
    .AddTo(Disposables);
```

## Reaction Scope

Reactions attached to the root observe all actions in the tree. Reactions attached to specific domains only observe actions on that subtree. This allows both global and local event handling.

## Use Cases

Reactions enable many patterns:

- **Validation** - Prevent invalid actions (cheating, illegal moves)
- **Logging** - Track all actions for debugging or replay
- **Side effects** - Trigger achievements, spawn enemies, update UI
- **Cascading actions** - One action triggers others
- **Statistics** - Count actions, track metrics
- **Game rules** - Enforce complex constraints without modifying actions

## Next Steps

- [Part 3: Domain Hierarchy](03-domain-hierarchy.md) — Build complex entity trees
- [Part 4: Observable Attributes](04-observable-attributes.md) — Reactive UI bindings

See **Example 2** for complete code demonstrating all reaction types.
