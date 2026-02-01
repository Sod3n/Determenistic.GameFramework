# Duck Typing (Structural Typing)

The DAR framework implements structural typing through automatic domain forwarding, enabling actions to work with any domain that has the required structure, regardless of its position in the inheritance hierarchy. This powerful feature allows you to write generic, reusable actions that operate on capabilities rather than concrete types.

## The Principle

The concept comes from the classic programming adage:

> "If it walks like a duck and quacks like a duck, it's a duck."

Applied to the DAR framework: **If a domain can be damaged, it will receive damage** — whether it's a player, enemy, bullet, or wall. The action doesn't care about the entity's class name or inheritance chain; it only cares whether the entity has the required properties and methods.

This approach prioritizes **what an object can do** over **what an object is**, leading to more flexible and maintainable code.

## How It Works

### Automatic Domain Forwarding

The framework includes a powerful feature where executing an action on any domain automatically searches the domain tree for the first compatible target. This means you don't need to manually find the right domain—the framework does it for you.

When you call `Execute()` on an action, the framework:

1. Searches the domain tree starting from the provided domain
2. Finds the first domain matching the action's target type
3. Executes the action on that domain
4. Returns gracefully if no matching domain is found

This automatic forwarding is built into the base `DARAction` class and works transparently for all actions.

### Example: Universal Damage System

Consider a damage system that needs to work with many different entity types. Instead of creating separate damage actions for each entity type, you define a single interface that describes the capability to be damaged:

```csharp
public interface IDamageable : IDomain
{
    int Health { get; set; }
}

public class DamageAction : DARAction<IDamageable, DamageAction>
{
    [JsonProperty] public int Amount { get; set; }
    
    protected override void ExecuteProcess(IDamageable target)
    {
        target.Health -= Amount;
    }
}
```

This single action now works with **any** domain that implements `IDamageable`, regardless of what else that domain might be or where it sits in the class hierarchy.

### Multiple Entity Types

The beauty of this approach becomes clear when you see how diverse entities can all share the same capability. A player character, an enemy, a destructible wall, and even a bullet can all be damaged using the exact same action:

```csharp
public class Player : BranchDomain, IDamageable
{
    public int Health { get; set; } = 100;
    public string Name { get; set; }
}

public class Enemy : BranchDomain, IDamageable
{
    public int Health { get; set; } = 50;
}

public class Wall : LeafDomain, IDamageable
{
    public int Health { get; set; } = 200;
}
```

Notice how `Player` and `Enemy` inherit from `BranchDomain` while `Wall` inherits from `LeafDomain`. Despite different base classes, they all work with the same damage action because they share the `IDamageable` interface.

The same action instance can damage any of these entities:

```csharp
var damage = new DamageAction { Amount = 25 };

damage.Execute(player);  // Works
damage.Execute(enemy);   // Works
damage.Execute(wall);    // Works
```

## Key Benefits

### Polymorphism Without Inheritance

Traditional object-oriented design requires entities to share a common base class to be treated polymorphically. The duck typing pattern eliminates this constraint. Entities from completely different inheritance hierarchies can be treated uniformly as long as they implement the same interface.

This is particularly valuable in game development where entities often have diverse base requirements. A player might need to be a `BranchDomain` to contain inventory and equipment, while a wall might be a simple `LeafDomain` with no children. Both can still be damageable without forcing an awkward inheritance structure.

### Composable Behaviors

Entities can implement multiple interfaces, mixing and matching capabilities as needed. This creates a flexible component-like system where behaviors are composed rather than inherited:

```csharp
public interface IMovable : IDomain
{
    Vector2 Position { get; set; }
}

public class Player : BranchDomain, IDamageable, IMovable
{
    public int Health { get; set; }
    public Vector2 Position { get; set; }
}

public class Enemy : BranchDomain, IDamageable, IMovable
{
    public int Health { get; set; }
    public Vector2 Position { get; set; }
}
```

Now both `Player` and `Enemy` can be damaged (via `DamageAction`) and moved (via `MoveAction`), but each class can have completely different implementations and additional capabilities unique to their role.

### Automatic Compatibility

When you add a new entity type to your game, it automatically works with all existing actions that target its interfaces. You don't need to update action code or create new action variants:

```csharp
public class Barrel : LeafDomain, IDamageable
{
    public int Health { get; set; } = 30;
}
```

The moment you define this class, all damage-related actions, reactions, and systems that work with `IDamageable` immediately support barrels. This dramatically reduces the coupling between systems and makes the codebase more maintainable.

### Type Safety with Graceful Degradation

The pattern maintains compile-time type safety while providing graceful runtime behavior. If you try to execute an action on an incompatible domain, the framework simply skips execution rather than throwing an error:

```csharp
public class Rock : LeafDomain { }  // No IDamageable

var rock = new Rock();
damage.Execute(rock);  // Silently skips - rock can't be damaged
```

This allows you to write generic code that attempts operations without needing extensive type checking.

## Real-World Example: Collision System

Consider a collision system that needs to handle impacts between various entity types. Some entities should take damage from collisions, others shouldn't. Using duck typing, you can write a single collision handler that works universally:

```csharp
public class CollisionAction : DARAction<IMovable, CollisionAction>
{
    [JsonProperty] public int OtherId { get; set; }
    
    protected override void ExecuteProcess(IMovable entity)
    {
        var other = Game.GetDomain(OtherId);
        
        // Attempt to damage the other entity
        new DamageAction { Amount = 10 }.Execute(other);
    }
}
```

This collision action works with any movable entity. When it tries to damage the other entity, the framework automatically checks if that entity implements `IDamageable`. If it does, damage is applied. If not, the damage action is safely skipped.

This means your collision system doesn't need to know about every entity type in your game. It just attempts damage, and the framework handles the rest. Whether the collision involves a player hitting an enemy, a bullet striking a wall, or a rock bouncing off terrain, the same code handles it all.

## Design Implications

This pattern fundamentally changes how you architect game systems. Instead of thinking in terms of entity hierarchies ("what things are"), you think in terms of capabilities ("what things can do"). This leads to:

**Smaller, focused actions**: Each action targets a specific capability interface rather than a broad entity type.

**Decoupled systems**: The damage system doesn't need to know about the movement system, inventory system, or AI system. Each operates on its own interfaces.

**Easier testing**: You can create minimal test domains that implement only the interfaces needed for testing, without building entire entity hierarchies.

**Better reusability**: Actions written for one game can often be reused in another game with different entity types, as long as the capability interfaces match.

## Summary

| Feature | Benefit |
|---------|---------||
| **Automatic forwarding** | Actions find compatible domains without manual searching |
| **Interface-based** | No common base class required |
| **Type-safe** | Compile-time guarantees with graceful runtime behavior |
| **Composable** | Entities mix multiple capabilities freely |
| **Extensible** | New entity types work with existing actions immediately |

The framework's duck typing enables flexible, reusable game logic that works across diverse entity types without tight coupling, making your codebase more maintainable and adaptable to changing requirements.
