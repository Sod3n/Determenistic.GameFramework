# GameLoop & Action Scheduling

The `GameLoop` is a fixed tick-rate update loop that lives in the domain tree as a `BranchDomain`. It drives all game logic — processors, scheduled actions, and tick-targeted work — on a single thread at a consistent rate.

## Basic Setup

Create a `GameLoop` with your root domain, set the tick rate, and start it. The loop runs on a background thread.

```csharp
var root = new RootDomain();
var gameLoop = new GameLoop(root);  // Adds itself as a subdomain of root
gameLoop.SetTickRate(60);           // 60 ticks per second (default)
_ = gameLoop.Start();
```

The `GameLoop` extends `BranchDomain`, so it participates in the domain tree and can host reactions (e.g., `IRequireTick` injection). See [Tick-Based Game Loop](08-tick-based-gameloop.md) for details on the tick system.

## Tick Rate

The tick rate controls how many fixed-step updates run per second. Each tick advances the simulation by exactly `1 / tickRate` seconds (`FixedDeltaTime`). The loop accumulates real elapsed time and runs as many ticks as needed to keep up, capped at 5 per frame to prevent spiral-of-death.

Lower tick rates reduce CPU usage for games that don't need frequent updates:
- **60 tick/s** — real-time action games with smooth movement
- **30 tick/s** — moderate update frequency, balanced performance
- **10 tick/s** — turn-based games with infrequent state changes

## Scheduling Actions

### `Schedule(Action)` — next-frame execution

Use `Schedule()` to queue actions that will execute at the start of the next frame, before any ticks run. The method uses a `ConcurrentQueue` to safely accept actions from any thread.

This is essential for thread safety when actions need to be triggered from:
- Network callbacks (SignalR hub methods)
- Timer events
- Async operations
- External threads

### `ScheduleOnTick(long tick, Action)` — tick-targeted execution

Use `ScheduleOnTick()` to queue work for a specific future tick. The action executes at the start of that tick, before processors run. If the tick has already passed, the action executes on the next tick.

This is how the client handles incoming server actions — each action arrives stamped with the tick it belongs to and gets scheduled for that exact tick.

## Thread Safety

The game loop ensures all scheduled actions and tick callbacks run sequentially on the game thread. When network callbacks receive player actions on different threads, schedule them to execute on the game thread.

This prevents race conditions when multiple clients send actions simultaneously. All actions execute in the order they were scheduled, maintaining deterministic behavior.

## Update Events

Subscribe to the `OnUpdate` event for custom per-tick logic. This event fires after tick-scheduled actions execute but before processors run. Use it for game-wide checks like win conditions or global timers.

## Processors

Domains can implement `IProcessor` to receive automatic per-tick updates. The interface requires:

- `Process(float delta, long currentTick)` — called every tick with fixed delta time and the current tick count
- `OnProcessorEnabled()` — called when processor is discovered in tree
- `OnProcessorDisabled()` — called when processor is removed from tree

The game loop automatically discovers all `IProcessor` domains in the tree, tracks their lifecycle, and calls `Process()` each tick. This is ideal for:

- Tick-based deadline checks (e.g., turn timers)
- Continuous movement or physics
- Resource regeneration over time
- AI behavior updates

**Note:** `IProcessor` updates are not automatically synced in multiplayer. Processors run independently on each client/server. For deterministic multiplayer, use processors to trigger actions (which are synced via `HistoryDomain`) rather than directly modifying state in `Process()`.

## Error Handling

The game loop isolates errors at multiple levels:

- **Frame-level errors** — caught to prevent loop crash
- **Scheduled action errors** — isolated so one failure doesn't affect others
- **Tick-scheduled action errors** — isolated per action with tick info in the log
- **Processor errors** — caught during processor execution
- **Update event errors** — caught when invoking listeners

All errors are logged with stack traces. The server continues running even if individual actions or processors fail, ensuring high availability.

## Use Cases

**Turn-based games:**
- Set low tick rate (10 tick/s) for minimal CPU usage
- Use `ScheduleOnTick` for turn transitions at specific ticks
- Use processors with `currentTick` for deadline checks

**Real-time games:**
- Set higher tick rate (60 tick/s) for smooth updates
- Implement `IProcessor` on domains that need continuous updates (movement, physics)
- Use `FixedDeltaTime` for consistent calculations across all machines

**Future actions:**
- Use `ScheduleOnTick(targetTick, ...)` for precise timing
- Convert seconds to ticks: `targetTick = CurrentTick + seconds * TickRate`
- Useful for timed events, spawn waves, or ability cooldowns

## Performance Considerations

- Lower tick rates reduce CPU usage for games with infrequent updates
- Scheduled actions execute sequentially — avoid long-running operations
- Processors run every tick — keep `Process()` methods lightweight
- Use reactions for event-driven logic instead of polling in processors

## Integration with Network

Combine with `HistoryDomain` for deterministic multiplayer. When network callbacks receive client actions, schedule them on the game thread for execution. The `HistoryDomain`'s After reaction automatically records executed actions.

Actions are stamped with `CurrentTick` so clients can schedule them on the correct tick using `ScheduleOnTick`. This ensures all actions execute deterministically at the same simulation point, maintaining consistency across clients. See [Network Game State](../network/01-network-game-state.md) for details on the history-based synchronization pattern.

## See Also

- [Tick-Based Game Loop](08-tick-based-gameloop.md) — fixed ticks, `IRequireTick`, `TickDelay`, `AdvanceToTick`
- [Network Threads](../network/05-network-threads.md) — thread safety in multiplayer
- [Time Synchronization](../network/04-time-synchronization.md) — coordinating timing across clients
