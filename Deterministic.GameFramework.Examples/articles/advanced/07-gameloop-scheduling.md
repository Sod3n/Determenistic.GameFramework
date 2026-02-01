# GameLoop & Action Scheduling

The `GameLoop` class (see `GameLoop.cs`) provides a fixed-timestep update loop for server-side game logic, with support for scheduling actions to run on the game thread.

## Basic Setup

Create a `GameLoop` instance with your root domain, set the target frame rate, and start it. The loop runs on a background thread and can be stopped when needed.

The default frame rate is 60 FPS, but you can adjust it based on your game's needs.

## Frame Rate Control

The game loop maintains a consistent frame rate using sleep-based timing (see `GameLoop.cs:47-71`). It measures actual elapsed time per frame and sleeps for the remaining duration to hit the target frame rate.

Lower frame rates reduce CPU usage for games that don't need frequent updates:
- **60 FPS** - Real-time action games with smooth movement
- **30 FPS** - Moderate update frequency, balanced performance
- **10 FPS** - Turn-based games with infrequent state changes

## Scheduling Actions

Use `Schedule()` (see `GameLoop.cs:97-101`) to queue actions that will execute on the next frame. The method uses a `ConcurrentQueue` to safely accept actions from any thread.

This is essential for thread safety when actions need to be triggered from:
- Network callbacks (SignalR hub methods)
- Timer events
- Async operations
- External threads

Scheduled actions are dequeued and executed sequentially during the update loop (see `GameLoop.cs:106-118`).

## Thread Safety

The game loop ensures all scheduled actions run sequentially on the game thread. When network callbacks receive player actions on different threads, schedule them to execute on the game thread.

This prevents race conditions when multiple clients send actions simultaneously. All actions execute in the order they were scheduled, maintaining deterministic behavior.

## Update Events

Subscribe to the `OnUpdate` event (see `GameLoop.cs:24`) for custom per-frame logic. This event fires after scheduled actions execute but before processors run. Use it for game-wide checks like win conditions or global timers.

## Processors

Domains can implement `IProcessor` (see `IProcessor.cs`) to receive automatic per-frame updates. The interface requires:

- `Process(float deltaTime)` - Called every frame with elapsed time
- `OnProcessorEnabled()` - Called when processor is discovered in tree
- `OnProcessorDisabled()` - Called when processor is removed from tree

The game loop automatically discovers all `IProcessor` domains in the tree (see `GameLoop.cs:143-180`), tracks their lifecycle, and calls `Process()` each frame. This is ideal for:

- Countdown timers that trigger actions
- Continuous movement or physics
- Resource regeneration over time
- AI behavior updates

**Note:** `IProcessor` updates are not automatically synced in multiplayer. Processors run independently on each client/server. For deterministic multiplayer, use processors to trigger actions (which are synced via `HistoryDomain`) rather than directly modifying state in `Process()`.

## Error Handling

The game loop isolates errors at multiple levels (see `GameLoop.cs:73-79`, `108-117`, `132-140`):

- **Frame-level errors** - Caught to prevent loop crash
- **Scheduled action errors** - Isolated so one failure doesn't affect others
- **Processor errors** - Caught during processor execution
- **Update event errors** - Caught when invoking listeners

All errors are logged with stack traces. The server continues running even if individual actions or processors fail, ensuring high availability.

## Use Cases

**Turn-based games:**
- Set low frame rate (10 FPS) for minimal CPU usage
- Schedule turn transitions when players submit moves
- Use processors for turn timers

**Real-time games:**
- Set higher frame rate (60 FPS) for smooth updates
- Implement `IProcessor` on domains that need continuous updates (movement, physics)
- Use `deltaTime` parameter for frame-rate independent calculations

**Delayed actions:**
- Use `Task.Delay()` with `ContinueWith()` to schedule future actions
- Combine with `Schedule()` to ensure thread-safe execution
- Useful for timed events, spawn waves, or ability cooldowns

## Performance Considerations

- Lower frame rates reduce CPU usage for games with infrequent updates
- Scheduled actions execute sequentially - avoid long-running operations
- Processors run every frame - keep `Process()` methods lightweight
- Use reactions for event-driven logic instead of polling in processors

## Integration with Network

Combine with `HistoryDomain` for deterministic multiplayer. When network callbacks receive client actions, schedule them on the game thread for execution. The `HistoryDomain`'s After reaction automatically records executed actions.

This ensures all actions execute deterministically in order, maintaining consistency across clients. See [Network Game State](../network/01-network-game-state.md) for details on the history-based synchronization pattern.

## See Also

- [Network Threads](../network/05-network-threads.md) - Thread safety in multiplayer
- [Time Synchronization](../network/04-time-synchronization.md) - Coordinating timing across clients
