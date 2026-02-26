# Serialization & Network IDs

To save game state or send it over the network, we need to convert our struct-based `GlobalState` into a byte array.

## The `[NetworkId]` Attribute

Every component struct MUST have a unique ID. This is critical for the framework to know *what* type of data is being serialized.

```csharp
[NetworkId(500)] // <--- This ID must be unique across your project
public struct Velocity : IComponent
{
    public Vector2 Value;
}
```

If you forget this attribute, the framework will throw an error at runtime (or compile time if using Source Generators).

## Snapshots

You can capture the entire state of the game in a highly optimized, compact byte array.

```csharp
// Capture
byte[] data = StateSerializer.Serialize(state);

// Restore
StateSerializer.Deserialize(newState, data);
```

### What gets serialized?
- **Global Variables**: `NextEntityId`, etc.
- **Entity Masks**: Which entities have which components.
- **Component Arrays**: The raw data of all components.

Because our components are **blittable structs** (they contain no references), the serializer can use `MemoryMarshal` to copy memory blocks extremely fast. It does not use slow reflection or JSON serialization.

## Optimization: BitMask128

The framework uses a 128-bit mask (`BitMask128`) to track which components an entity has. This allows for O(1) checks.

- `HasComponent<T>(entity)` is a simple bit check.
- `Filter<T1, T2>()` uses SIMD-friendly bitwise AND operations to find matching entities instantly.

This is significantly faster than `HashSet<T>` or dictionary lookups used in traditional engines.
