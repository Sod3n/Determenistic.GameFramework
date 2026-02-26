# Best Practices

Building a deterministic game requires discipline. Here are the golden rules to ensure your game stays in sync and performs well.

## 1. The "No Floats" Rule is Absolute

**Never** cast a `Float` to a `float` for calculation, even for "simple" things.
Even `(float)myFixedVal * 0.5f` is dangerous because `0.5f` might be represented differently on different platforms.

- **Bad**: `position.X += (Float)((float)velocity.X * 0.1f);`
- **Good**: `position.X += velocity.X * new Float(0.1f);`

## 2. Separate Logic from View

Your `GlobalState` should only contain data relevant to the *gameplay rules*.
- **Include**: Health, Position, Ammo, Cooldowns.
- **Exclude**: Particle effect IDs, Animation frames, UI state, Sound handles.

Use **Reactions** or the `OnTick` event to bridge the gap.
For example, when `Health` changes, fire an event that your Unity/Godot/Monogame frontend listens to update the UI.

## 3. Keep Components Small & Flat

The serializer is fastest when copying contiguous memory.
- Prefer many small components (`Position`, `Velocity`, `Health`) over one giant `PlayerGodObject` component.
- This also makes `Filter<T>` queries more efficient.

## 4. Don't Store References

Never store a `class` inside an `IComponent`.
If you need to reference another entity, store its `Entity` ID (or use the `Ref` wrapper struct).

- **Bad**: `public Player Owner;`
- **Good**: `public Entity OwnerId;` or `public Ref Owner;`

## 5. Use BitMasks for Flags

Instead of multiple `bool` fields in a component (which each take 1 byte minimum), use a `BitMask128` or a simple `int` flags enum if you have many boolean states.
However, for simple gameplay flags, a `bool` in a struct is perfectly fine and safe.

## 6. Pre-allocate Everything

The framework is designed to be zero-allocation during gameplay.
- Don't create `new List<T>()` inside your Action execution.
- If you need a temporary list, use a pooled list or a fixed-size buffer like `Span<T>` with `stackalloc`.

## 7. Deterministic Iteration

When iterating over collections (like a `Dictionary` or `HashSet`), the order is **undefined** and can vary.
Since `GlobalState` uses arrays and `BitMask` iteration, `Filter<T>` is guaranteed to return entities in ID order (0, 1, 2...).

If you implement your own collections in a component (e.g., a spatial hash grid), ensure your query iteration order is deterministic (e.g., sort by Entity ID).

## 8. Debugging Desyncs

If you suspect a desync:
1. Enable `StateHistory` logging.
2. Compare the `StateHash` (if implemented) or the serialized bytes of the state between two clients at the same tick.
3. The first tick where they differ is where the non-deterministic logic happened.
