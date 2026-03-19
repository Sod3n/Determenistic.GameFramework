# Part 3: Rollback Networking

Rollback networking handles late-arriving network packets by rewinding time, applying the late input, and resimulating forward.

## Why Rollback?

Without rollback, you must either:
- Wait for all inputs before simulating (high latency)
- Skip late inputs (unfair gameplay)

With rollback:
- Simulate immediately with available inputs (low latency)
- Rewind and resimulate when late inputs arrive (correct gameplay)

## How It Works

The framework automatically:

1. **Saves state** - Stores snapshots every tick (ring buffer, ~1 second)
2. **Detects late packets** - Identifies actions arriving for past ticks
3. **Rolls back** - Restores state to just before the late action
4. **Resimulates** - Replays all actions from that point forward

## State History

```csharp
var gameLoop = new GameLoop(state, dispatcher, scheduler);
gameLoop.SetTickRate(60); // Stores last 60 snapshots
```

## Scheduling Actions

```csharp
gameLoop.Schedule(new DamageAction(10), player);           // Next tick
gameLoop.ScheduleOnTick(5, new DamageAction(15), player);  // Specific tick
```

## Automatic Rollback

When a late packet arrives for tick 5 but current tick is 10:

```csharp
scheduler.ScheduleFromBytes(actionTypeId, actionBytes, entityId, tick: 5);
gameLoop.RunSingleTick(); // Triggers rollback automatically
```

The framework:
1. Rewinds to tick 4
2. Restores state from history
3. Resimulates ticks 5-11 with the late action included

## Serialization

State serialization uses `Marshal.Copy` for maximum performance:

```csharp
byte[] snapshot = StateSerializer.Serialize(state);
StateSerializer.Deserialize(newState, snapshot);
```

## Determinism Requirements

Rollback only works if your game is deterministic:

- **Fixed-point math** - `Float` instead of `float`
- **Deterministic random** - `DeterministicRandom` with seeds
- **Safety analyzers** - Compile-time checks for non-deterministic types
- **Blittable components** - No reference types in `IComponent` structs

## Next Steps

- [Advanced: Best Practices](../advanced/best-practices.md) - Optimization tips
- [Advanced: Testing](../advanced/testing.md) - Unit testing deterministic games

See `PoCTest.cs` for the complete rollback demonstration.
