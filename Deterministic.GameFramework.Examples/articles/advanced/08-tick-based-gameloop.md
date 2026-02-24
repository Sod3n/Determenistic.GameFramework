# Tick-Based Game Loop

The `GameLoop` uses a **fixed tick rate** — every tick advances the simulation by the same amount of time, regardless of how fast or slow the real clock runs. This is the foundation for deterministic multiplayer: if two machines process the same actions on the same ticks, they produce identical results.

## Why Fixed Ticks?

A variable delta-time loop (`Update(float dt)`) is simple but non-deterministic. Floating-point math produces slightly different results depending on frame timing, and those differences compound over time.

A fixed tick-rate loop eliminates this. Every tick is exactly `1 / tickRate` seconds. The loop accumulates real elapsed time and runs as many fixed ticks as needed to catch up. If the machine is fast, it sleeps. If it's slow, it runs multiple ticks per frame (capped to prevent spiral-of-death).

**Key properties:**
- **`CurrentTick`** — a monotonically increasing counter, starting at 0
- **`TickRate`** — ticks per second (default 60)
- **`FixedDeltaTime`** — seconds per tick (`1 / TickRate`)

## Tick as a Unit of Time

Think of `CurrentTick` as your game clock. Instead of measuring time in seconds or milliseconds, you measure it in ticks. Converting between the two is straightforward:

- Ticks to seconds: `ticks * FixedDeltaTime`
- Seconds to ticks: `seconds * TickRate`

For example, a 30-second turn timer at 60 tick/s is `30 * 60 = 1800` ticks. Set the deadline to `CurrentTick + 1800`, and check each tick if the deadline has passed.

## Stamping Actions with Ticks

Every action can carry a `Tick` value — the tick on which it was created or should be executed. This serves two purposes:

1. **Ordering** — actions are tied to a specific point in the simulation timeline
2. **Scheduling** — the client can schedule an action to execute on the exact tick the server intended

The `GameLoop` lives in the domain tree as a `BranchDomain`. It registers a reaction that auto-injects tick information into any action implementing `IRequireTick`:

```csharp
public class MyAction : DARAction<MyDomain, MyAction>, IRequireTick
{
    public long CurrentTick { get; set; }  // Injected automatically
    public int TickRate { get; set; }      // Injected automatically

    protected override void ExecuteProcess(MyDomain domain)
    {
        // Use CurrentTick and TickRate for time-based logic
        var deadlineTick = CurrentTick + 30 * TickRate; // 30 seconds from now
    }
}
```

No manual wiring needed — if the `GameLoop` is in the domain tree (which it is by default in both `ServerDomain` and `ClientDomain`), the injection happens automatically via the reaction pipeline.

## Scheduling Actions on a Tick

Use `ScheduleOnTick(long tick, Action action)` to queue work for a specific future tick. The action executes at the start of that tick, before processors run.

This is how the client handles incoming server actions — each action arrives stamped with the tick it belongs to, and gets scheduled for that exact tick:

```csharp
gameLoop.ScheduleOnTick(action.Tick, () => action.Execute(gameState));
```

If the tick has already passed, the action executes on the next tick.

## Processors and Ticks

Domains implementing `IProcessor` receive both `delta` and `currentTick` in their `Process` method:

```csharp
public class BattleDomain : BranchDomain, IProcessor
{
    public void Process(float delta, long currentTick)
    {
        if (TurnDeadlineTick.Value > 0 && currentTick >= TurnDeadlineTick.Value)
        {
            new EndTurnAction().Execute(this);
        }
    }
}
```

The `delta` is always `FixedDeltaTime` (constant), and `currentTick` is the authoritative game clock. Prefer comparing ticks over accumulating deltas — it's exact and doesn't drift.

## Client Tick Delay

The client can run its game loop a few ticks behind the server, creating a buffer for network actions to arrive before they're needed:

```csharp
GameLoop.TickDelay = 3; // 3 ticks of buffer (~50ms at 60 tick/s)
```

The loop starts with a negative accumulator, so no ticks fire until the delay has elapsed. Scheduled actions still queue normally during this window. This is a simple form of input buffering that smooths out network jitter without adding complexity.

## Replay with AdvanceToTick

For state synchronization (e.g., a late-joining client), the `GameLoop` can fast-forward synchronously:

```csharp
gameLoop.AdvanceToTick(targetTick);
```

This runs every tick from `CurrentTick` to `targetTick`, draining scheduled actions and running processors at each step. Combined with `ScheduleOnTick`, this means you can schedule an entire action history onto their original ticks and replay the full simulation deterministically.

## Summary

| Concept | What it does |
|---|---|
| **Fixed tick rate** | Every tick is the same duration — deterministic by design |
| **CurrentTick** | The game clock — use it instead of wall-clock time |
| **IRequireTick** | Auto-injects `CurrentTick` and `TickRate` into actions |
| **ScheduleOnTick** | Queues work for a specific future tick |
| **TickDelay** | Client-side buffer for network smoothing |
| **AdvanceToTick** | Synchronous fast-forward for replay/resync |

## See Also

- [GameLoop & Action Scheduling](07-gameloop-scheduling.md) — general scheduling and thread safety
- [Determinism](02-determinism.md) — why fixed ticks matter for multiplayer
- [Network Game State](../network/01-network-game-state.md) — history-based synchronization
