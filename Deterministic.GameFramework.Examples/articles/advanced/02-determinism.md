# Determinism

Ensuring identical game state across server and all clients is fundamental to building efficient multiplayer games. This article explains why determinism matters and how the framework guarantees it.

## Why Determinism Matters

In traditional multiplayer architectures, the server sends every state change to every client. When a player plays a card, the server sends messages like "Player drew card X", "Enemy took 10 damage", "Buff applied", etc. This creates significant network traffic and latency.

The DAR framework uses a more efficient approach: **send only player inputs, execute logic locally**. Instead of transmitting all state changes, we send just the action that triggered them. Each client executes the action locally and arrives at the same result.

### The Efficiency Gain

Consider a `PlayCardAction` that triggers a complex chain of events:
- Draw 2 cards (randomly selected from deck)
- Deal damage to 3 enemies
- Trigger 5 reactions (buffs, debuffs, animations)
- Create new domain entities (each needing an ID)
- Update 10+ different state values

In a traditional architecture, this requires sending 20+ network messages. With deterministic execution, we send **one action** containing just the card ID. Each client executes the action locally and produces identical results.

This dramatically reduces network bandwidth and latency, making real-time multiplayer feasible even with complex game logic.

### The Challenge: Non-Determinism

This efficiency only works if execution is **deterministic** — meaning the same action produces the same result on every machine. Non-deterministic operations cause **desync**, where client state diverges from server state, breaking the game.

Common sources of non-determinism include:

**Random numbers**: Using different random seeds produces different results. If the server's random generator produces 7 but the client's produces 3, they'll diverge.

**GUIDs**: Calling `Guid.NewGuid()` generates different values on each machine. If entities get different IDs, references break.

**Dictionary iteration**: Dictionary order is not guaranteed. Iterating and processing items in different orders causes divergence.

**Floating point arithmetic**: Can vary slightly across platforms and CPU architectures, accumulating errors over time.

**System time**: Using `DateTime.Now` gives different values on each machine.

Any of these will cause desync, making the game unplayable in multiplayer.

## The Solution

The framework provides two domains that ensure determinism:

| Domain | Problem Solved |
|--------|----------------|
| `IdProviderDomain` | Assigns sequential IDs instead of GUIDs |
| `RandomProviderDomain` | Provides seeded random numbers |

## IdProviderDomain: Deterministic Entity IDs

The first major source of non-determinism is entity ID generation. Many developers instinctively use `Guid.NewGuid()` for unique identifiers, but this generates different values on each machine, immediately causing desync.

The `IdProviderDomain` solves this by using a simple counter that increments for each new entity. Since all clients execute the same actions in the same order, they all assign the same IDs to the same entities.

```csharp
public class MyGameState : BaseGameState
{
    public IdProviderDomain IdProvider { get; }
    
    public MyGameState(Guid matchId) : base(matchId, randomSeed: 0)
    {
        IdProvider = new IdProviderDomain(this);
    }
}
```

### How It Works

The `IdProviderDomain` uses the reaction system to automatically assign IDs to new entities:

1. It subscribes to `AddSubdomainAction` events on the root domain
2. Whenever any domain is added anywhere in the tree, the reaction triggers
3. It assigns the next sequential number as the domain's ID
4. The counter increments for the next entity

This happens automatically and transparently. You don't need to manually assign IDs—just create domains normally and they receive sequential IDs:

```csharp
var player = new PlayerDomain(gameState, "Hero");
var enemy = new EnemyDomain(gameState, "Goblin");

Console.WriteLine(player.Id);  // 1
Console.WriteLine(enemy.Id);   // 2
```

Since all clients execute the same actions (creating the same domains in the same order), they all assign the same IDs. Player is always ID 1, enemy is always ID 2, on every machine.

### Benefits

**Deterministic**: Same actions produce same IDs across all clients, preventing desync.

**Network efficient**: Small integer IDs (1, 2, 3...) are much more compact than GUIDs for network transmission.

**Debuggable**: Sequential IDs make logs and debugging much easier than random GUIDs.

**Simple**: No complex ID coordination or synchronization needed.

## RandomProviderDomain: Deterministic Randomness

The second major source of non-determinism is random number generation. Games need randomness for card draws, damage variance, critical hits, etc. But if each client uses its own random seed, they'll generate different random numbers and desync.

The `RandomProviderDomain` solves this by providing a seeded random number generator that all clients initialize with the same seed. Since they start with the same seed and execute the same actions in the same order, they generate the same random sequence.

```csharp
public class MyGameState : BaseGameState
{
    public RandomProviderDomain RandomProvider { get; }
    
    public MyGameState(Guid matchId, int seed) : base(matchId, seed)
    {
        RandomProvider = new RandomProviderDomain(this, seed);
    }
}
```

### Using Random in Actions

Actions that need randomness implement the `IRequireRandom` interface. The `RandomProviderDomain` automatically injects a `Random` instance using the [action injection pattern](01-action-injection.md):

```csharp
public class DamageAction : DARAction<EnemyDomain, DamageAction>, IRequireRandom
{
    public Random Random { get; set; }  // Injected
    
    public int BaseDamage { get; set; }
    
    protected override void ExecuteProcess(EnemyDomain enemy)
    {
        var multiplier = 0.8 + (Random.NextDouble() * 0.4);
        var actualDamage = (int)(BaseDamage * multiplier);
        enemy.Health -= actualDamage;
    }
}
```

The injection happens in the Prepare phase before the action executes. Since all clients use the same seed and call `Random.NextDouble()` in the same order (because they execute the same actions), they all get the same random values.

### The Critical Requirement

This only works if you **never** use `Random.Shared` or create your own `Random` instances in game logic. All randomness must go through the injected `Random` instance. Breaking this rule will cause desync.

## Seed Coordination

For multiplayer games, all clients must start with the same random seed. The typical approach is to derive the seed from the match ID, which is shared by all clients:

```csharp
public class MyGameState : BaseGameState
{
    public MyGameState(Guid matchId) : base(matchId, GenerateSeed(matchId))
    {
        // ...
    }
    
    private static int GenerateSeed(Guid matchId)
    {
        return BitConverter.ToInt32(matchId.ToByteArray(), 0);
    }
}
```

Since all clients know the match ID, they can all independently compute the same seed. This eliminates the need to transmit the seed separately.

## Detecting Desync: DeterminismValidator

Even with careful design, bugs can introduce non-determinism. The framework includes `DeterminismValidator` to detect desync during development and testing.

The validator works by running two parallel simulations of the game state and comparing their hashes after each action. If the hashes differ, you've found non-deterministic code:

```csharp
DeterminismValidator<MyGameState>.IsEnabled = true;

DeterminismValidator<MyGameState>.OnDeterminismFailure += 
    (matchId, actionType, actionJson, primaryHash, shadowHash, diff) =>
    {
        Console.WriteLine($"DESYNC in {matchId}: {diff}");
    };
```

When desync is detected, the validator provides detailed information about which action caused the divergence and what state differences occurred. This makes debugging much easier than trying to track down desync in production.

You should enable the validator during development and in your test environment. It has performance overhead, so disable it in production once you've verified determinism.

## Best Practices for Determinism

Following these rules will keep your game deterministic:

**Never use `Guid.NewGuid()` in game logic**: Always use domain IDs assigned by `IdProviderDomain`. GUIDs are fine for match IDs or other server-side identifiers that aren't part of game state.

**Never use `Random.Shared` or `new Random()`**: All randomness must go through the injected `Random` from `RandomProviderDomain`. Implement `IRequireRandom` on any action that needs randomness.

**Avoid `DateTime.Now` or `DateTime.UtcNow`**: These give different values on each machine. Instead, pass time as an action parameter or maintain a game clock that advances deterministically.

**Use ordered collections**: Prefer `List<T>` over `HashSet<T>` or `Dictionary<K,V>` when iteration order matters. Dictionary iteration order is not guaranteed and can vary between machines.

**Avoid floating point when possible**: If you must use floating point, be aware that results can vary slightly across platforms. Consider using fixed-point arithmetic for critical calculations.

**Test with `DeterminismValidator`**: Enable the validator during development to catch non-determinism early. It's much easier to fix desync bugs when you know exactly which action caused them.

**Be careful with LINQ**: Some LINQ operations like `ToDictionary()` or operations on `HashSet<T>` can produce non-deterministic ordering. Always use `.OrderBy()` when order matters.

## Summary

| Component | Purpose |
|-----------|---------||
| `IdProviderDomain` | Assigns sequential IDs instead of GUIDs |
| `RandomProviderDomain` | Provides seeded random number generator |
| `IRequireRandom` | Interface for actions needing randomness |
| `DeterminismValidator` | Detects state divergence during testing |

Determinism is the foundation of efficient multiplayer in the DAR framework. By following these patterns and avoiding common pitfalls, you can build complex multiplayer games that stay perfectly synchronized across all clients with minimal network traffic.

## Next Steps

- [Network Actions](../network/02-network-actions.md) — Send actions between client and server
