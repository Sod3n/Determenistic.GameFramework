# Action Injection & Domain References

In networked games, actions must be serializable to send between client and server. This creates a fundamental challenge: actions need access to game state and domain objects, but object references cannot be serialized over the network.

## The Problem

When an action needs to reference another domain (like attacking a target), you cannot directly store the object reference because JSON serialization cannot handle object graphs. Attempting to serialize an object reference will either fail or create a massive payload with the entire object tree.

The naive approach of storing object references doesn't work:

```csharp
// ❌ Can't serialize object references
public class AttackAction
{
    public Unit Target { get; set; }  // This won't serialize properly
}
```

Instead, you must use primitive identifiers that can be serialized:

```csharp
// ✅ Use IDs instead
public class AttackAction
{
    [JsonProperty] public int TargetId { get; set; }
}
```

This solves the serialization problem, but creates a new challenge: **how does the action resolve the ID back to the actual domain object at execution time?**

## The Solution: Dependency Injection Pattern

The framework provides an elegant solution using the reaction system to inject dependencies into actions before they execute. This pattern has two components working together:

1. **ID-based references** for network serialization
2. **Automatic injection** of game state for ID resolution

### Step 1: Define an Injection Interface

Create an interface that declares what the action needs injected. This interface extends `IDARAction` to mark it as an action requirement:

```csharp
public interface IRequireGame : IDARAction
{
    GameState Game { get; set; }
}
```

Any action implementing this interface signals that it needs the `GameState` injected before execution.

### Step 2: Create the Injector

The game state domain sets up a reaction that automatically injects itself into any action implementing the injection interface. This reaction runs in the **Prepare** phase, which occurs before the action executes:

```csharp
public class GameState : BranchDomain
{
    private readonly Dictionary<int, LeafDomain> _registry = new();
    
    public GameState()
    {
        // Inject this GameState into any action that needs it
        new Reaction<LeafDomain, IRequireGame>(this)
            .Prepare((_, action) => action.Game = this)
            .AddTo(Disposables);
    }
    
    public LeafDomain? GetDomain(int id) => 
        _registry.TryGetValue(id, out var domain) ? domain : null;
}
```

The injector reaction watches for any action implementing `IRequireGame` and automatically populates the `Game` property before the action executes. This happens transparently without the action needing to request it explicitly.

### Step 3: Use in Actions

Actions implement the injection interface and use both serializable IDs and injected dependencies:

```csharp
public class AttackAction : DARAction<Unit, AttackAction>, IRequireGame
{
    [JsonIgnore] public GameState Game { get; set; }  // Injected (not serialized)
    
    [JsonProperty] public int TargetId { get; set; }  // Serialized
    
    protected override void ExecuteProcess(Unit attacker)
    {
        // Use injected Game to resolve ID to actual domain
        var target = Game.GetDomain(TargetId) as Unit;
        if (target == null) return;
        
        target.Health -= attacker.Attack;
    }
}
```

The `[JsonIgnore]` attribute prevents the injected `Game` property from being serialized, while `[JsonProperty]` ensures the `TargetId` is included in network transmission.

## How It Works

The execution flow demonstrates the elegance of this pattern:

1. **Client creates action** with only the target ID: `new AttackAction { TargetId = 5 }`
2. **Action serializes** to JSON: `{"TargetId": 5}` (Game property excluded)
3. **Server receives** and deserializes the action
4. **Prepare phase runs** → Injector reaction populates `Game` property
5. **Action executes** → Uses injected `Game` to resolve ID 5 to the actual Unit
6. **Target takes damage** → Game state updates

This pattern ensures actions remain lightweight for network transmission while still having full access to game state at execution time.

## Benefits

**Network Efficiency**: Only primitive IDs are transmitted, keeping network payloads small and fast.

**Type Safety**: The injection interface provides compile-time guarantees that actions declare their dependencies.

**Decoupling**: Actions don't need to know how to obtain game state—it's automatically provided.

**Testability**: You can easily inject mock game states for testing without network infrastructure.

**Consistency**: The same action code works on both client and server, with injection happening automatically on both sides.

## Common Injection Interfaces

You can create multiple injection interfaces for different dependencies:

- `IRequireGame` - Injects game state for domain lookups
- `IRequireRandom` - Injects seeded random number generator
- `IRequireTime` - Injects current game time
- `IRequirePlayer` - Injects the player who initiated the action

Each interface follows the same pattern: declare the property, create an injector reaction, and the framework handles the rest.

## Summary

| Component | Purpose |
|-----------|---------|
| `[JsonProperty]` | Marks IDs and primitives for network serialization |
| `[JsonIgnore]` | Excludes injected dependencies from serialization |
| `IRequire*` interface | Declares what dependencies an action needs |
| `.Prepare()` reaction | Injects dependencies before action execution |
| ID resolution | Converts serializable IDs back to domain objects |

This pattern is fundamental to building networked games with the DAR framework, enabling actions to be both network-friendly and fully functional.
