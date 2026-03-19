# Source Generators & Analyzers

The framework uses Roslyn Source Generators to eliminate runtime reflection and enforce deterministic coding standards.

## 1. StableId Registry

To avoid slow reflection at startup, the framework generates a `StableIdRegistry` class at compile time.

This maps `Type` <-> `int` ID instantly.

```csharp
// Generated code (conceptual)
public static class StableIdRegistry
{
    public static readonly Dictionary<Type, int> TypeToId = new()
    {
        { typeof(Position), 100 },
        { typeof(Velocity), 500 },
    };
}
```

This happens automatically when you add the `[StableId]` attribute.

## 2. Safety Analyzers (DGF200)

The most dangerous bug in a deterministic game is using non-deterministic types. If one client uses `float` and another uses `float` slightly differently, the game breaks.

We include a custom Roslyn Analyzer (`DGF200`) that runs **inside your IDE**.

It checks every field in your `IComponent` and `IAction` structs.

### What it flags:
- `float`, `double`
- `string` (use `FixedString32`)
- Classes (use `struct` or `Entity`)
- Arrays (use `List8<T>`)
- `List<T>`, `Dictionary<T,U>`

### Example Error
```csharp
public struct UnsafeComponent : IComponent
{
    public float Speed; // ERROR DGF200: Field 'Speed' is of non-deterministic type 'float'
}
```

This catches bugs **before you even compile**.
