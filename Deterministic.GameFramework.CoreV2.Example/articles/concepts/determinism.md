# Deterministic Math & Types

To ensure that your game runs identically on all machines, **Deterministic.GameFramework.CoreV2** provides a set of custom types that replace standard C# primitives.

## The Golden Rule: No Floats!

**NEVER** use `float` or `double` in your game logic or components.
Standard floating-point math is not deterministic across different hardware architectures.

### Instead, use `Float`

`Float` is a custom struct that implements **Q32.32 Fixed-Point Math**. It behaves like a float but guarantees identical results everywhere.

```csharp
// ❌ BAD: Non-deterministic
float health = 100.0f;
health -= 33.3f; 

// ✅ GOOD: Deterministic
Float health = new Float(100);
health -= new Float(33.3f);
```

The framework provides all standard math operations for `Float`:
- `Float.Sqrt(val)`
- `Float.Sin(val)`, `Float.Cos(val)`
- `Float.Abs(val)`, `Float.Min(a, b)`, `Float.Max(a, b)`
- `Float.Lerp(a, b, t)`

## Vector Types

For 2D and 3D math, use `Vector2` and `Vector3`. These are built on top of `Float`.

```csharp
Vector2 position = new Vector2(10, 5);
Vector2 velocity = new Vector2(1, 0);

// Deterministic vector math
position += velocity * new Float(0.5f);
Float dist = position.Magnitude;
```

## Random Numbers

Standard `System.Random` is not guaranteed to be deterministic across platforms or .NET versions.
Use `DeterministicRandom` instead. It is a component that can be stored in your game state.

```csharp
// Inside a system or action
var rng = state.GetState<DeterministicRandom>(rngEntity);

// Generate random values
Float chance = rng.NextFloat(); // 0.0 to 1.0
Int damage = rng.NextInt(10, 20); // 10 to 19
```

## Collections

Standard `List<T>` and `Dictionary<K,V>` are **reference types** and allocate memory. They are **not allowed** in networked components because they cannot be easily snapshotted or rolled back.

### Use `List8<T>`
For small lists inside components, use `List8<T>`. It is a fixed-size struct that holds up to 8 items.

```csharp
public struct Inventory : IComponent
{
    public List8<int> ItemIds; // Stores up to 8 item IDs directly in the struct
}
```

### Use `FixedString32`
For strings, use `FixedString32`. It holds a UTF-8 string of up to 32 bytes without allocating.

```csharp
public struct PlayerName : IComponent
{
    public FixedString32 Name;
}
```

## Summary Table

| Standard C# | **Deterministic Framework** |
|Data Type|Replacement|
|---|---|
|`float` / `double`|`Float`|
|`int`|`Int` (Optional wrapper, `int` is also safe)|
|`Vector2` (Unity/Numerics)|`Deterministic.GameFramework.CoreV2.Vector2`|
|`Random`|`DeterministicRandom`|
|`string`|`FixedString32`|
|`List<T>`|`List8<T>`|
|`Object` / `class`|`Entity` ID / `struct`|

> **Note:** The `Int` wrapper struct is available if you want to ensure type safety, but standard C# `int` is deterministic and safe to use in logic. However, for `IComponent` fields, we often prefer the framework types to be explicit.
