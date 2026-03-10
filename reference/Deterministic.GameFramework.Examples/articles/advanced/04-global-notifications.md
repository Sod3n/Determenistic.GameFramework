# Global Notifications via Root Domain

When building modular systems, you often need cross-domain communication without tight coupling. This pattern enables Domain B to react to Domain A's actions without either knowing about the other.

## The Pattern

**Architecture:**
```
Root
├── Domain A (publishes actions)
└── Domain B (subscribes via root)
```

Domain B subscribes to actions by attaching reactions to the root domain. When Domain A executes an action on itself, the action propagates up the tree to root, triggering Domain B's reactions automatically.

## Why This Works

The reaction system in the DAR architecture automatically propagates actions up the domain tree. When any domain executes an action, all reactions attached to parent domains (including root) are collected and executed. This means:

- Root domain sees **all actions** from anywhere in the tree
- Multiple domains can independently subscribe to the same actions
- Publishers and subscribers remain completely decoupled
- The pattern works seamlessly with network replication

## Implementation Steps

### Step 1: Domain A Publishes

Domain A executes actions on itself, completely unaware of any subscribers:

```csharp
public class DomainA : BranchDomain
{
    public void DoSomething(string message)
    {
        new SomethingHappenedAction { Message = message }.Execute(this);
    }
}
```

### Step 2: Domain B Subscribes

Domain B gets a reference to root and attaches reactions to it:

```csharp
public class DomainB : BranchDomain
{
    public DomainB(BranchDomain parent) : base(parent)
    {
        var root = GetInParent<RootDomain>(includeSelf: false);
        
        new Reaction<LeafDomain, SomethingHappenedAction>(root)
            .After((_, action) => 
            {
                Console.WriteLine($"Received: {action.Message}");
            })
            .AddTo(Disposables);
    }
}
```

### Step 3: Define the Action

Actions serve as typed event messages. Their `ExecuteProcess` can be empty since reactions do the actual work:

```csharp
public class SomethingHappenedAction : DARAction<LeafDomain, SomethingHappenedAction>
{
    public string Message { get; set; }
    
    protected override void ExecuteProcess(LeafDomain domain) { }
}
```

## Use Cases

This pattern is ideal for:

- **Cross-system coordination**: Battle system notifies UI system of state changes
- **Achievement tracking**: Game actions trigger achievement checks without coupling
- **Analytics/telemetry**: Centralized event logging without polluting game logic
- **Network broadcasting**: Server broadcasts events to all connected clients
- **Plugin architectures**: New features can subscribe to existing events without modifying publishers

## Key Benefits

**Decoupling**: Domain A has zero knowledge of Domain B. They can be developed, tested, and deployed independently.

**Scalability**: Any number of domains can subscribe to the same action. Adding a new subscriber doesn't require changes to existing code.

**Type Safety**: Actions are strongly typed, providing compile-time guarantees about event data structure.

**Network Compatible**: Actions can be serialized and replicated across network boundaries, making this pattern work in multiplayer scenarios.

**Testability**: Each domain can be tested in isolation. Mock subscribers can verify that correct actions are published.

## Common Patterns

### Multiple Independent Subscribers

Different systems can react to the same event for different purposes:

```csharp
// Achievement system tracks progress
new Reaction<LeafDomain, BattleEndAction>(root)
    .After((_, action) => TrackAchievements(action))
    .AddTo(achievementSystem.Disposables);

// Analytics system logs telemetry
new Reaction<LeafDomain, BattleEndAction>(root)
    .After((_, action) => LogEvent(action))
    .AddTo(analyticsSystem.Disposables);
```

### Filtering Subscriptions

Subscribers can filter which events they care about using conditional logic in the reaction handler, or by using the Abort phase to skip processing.

## Comparison to Traditional Events

Traditional C# events (`event Action<T>`) have limitations in this architecture:

- Events don't serialize over network
- Events require direct references between objects
- Events can't be logged or replayed
- Events don't integrate with the DAR execution pipeline

The action-based notification pattern solves all these issues while maintaining the same pub-sub semantics.

## Summary

| Component | Role |
|-----------|------|
| Domain A | Executes actions on itself |
| Domain B | Gets root via `GetInParent<RootDomain>()` |
| Root | Central hub where reactions are attached |
| Action | Typed message carrying event data |
| Reaction | Handler attached to root that processes events |

This pattern transforms the domain tree into a natural event bus, leveraging the existing reaction propagation system for clean, decoupled cross-domain communication.
