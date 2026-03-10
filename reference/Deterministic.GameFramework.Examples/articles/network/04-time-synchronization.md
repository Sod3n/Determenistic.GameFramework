# Time Synchronization

In multiplayer games, clients need synchronized time for coordinating animations, cooldowns, turn timers, and timed events. `NetworkTime` provides server-synchronized UTC time using an NTP-like algorithm.

## Why Time Sync Matters

Without synchronized time, clients experience desync issues where turn timers expire at different times, animation drift where timed animations don't align across clients, cooldown inconsistencies where abilities become available at different moments, and event timing errors where server events trigger at unexpected times on clients.

`NetworkTime` solves this by maintaining a calculated offset between server and client clocks.

## Basic Usage

Create a `NetworkTime` instance after connecting and wait for initial sync:

```csharp
var networkTime = new NetworkTime(networkClient);
while (!networkTime.IsSynced) await Task.Delay(10);
DateTime serverTime = networkTime.UtcNow;
```

The constructor accepts an optional sync interval (default: 30 seconds). Pass `TimeSpan.FromSeconds(10)` for more frequent syncing.

## How It Works

`NetworkTime` uses a simplified NTP algorithm. The client records local time `t0` when sending a sync request, the server responds with its UTC time `ts`, and the client records `t1` when the response arrives. The offset is calculated as `ts - ((t0 + t1) / 2)`, assuming symmetric network latency.

For example: if `t0 = 1000`, `ts = 1500`, and `t1 = 1100`, then `offset = 1500 - 1050 = +450 ticks` (client is 450 ticks behind server).

The offset is applied to local time via `UtcNow` property. See the [NetworkTime.cs source](../../TurnBasedPrototype.Shared.Network/NetworkTime.cs) for implementation details.

## Reactive Updates

Subscribe to `OnTimeSynced` to react when time synchronizes. This is useful for updating UI elements that display server time, recalculating turn timer countdowns, and adjusting scheduled events.

```csharp
networkTime.OnTimeSynced.Subscribe(offsetTicks => {
    // Update UI, recalculate timers, etc.
});
```

Force immediate sync with `SyncNow()` after reconnecting or when detecting significant drift.

## Engine-Agnostic Design

`NetworkTime` works with any C# game engine. Create it in your initialization method (Godot's `_Ready()`, Unity's `Start()`, MonoGame's `Initialize()`), then check `IsSynced` before using `UtcNow` in your update loop:

```csharp
// Initialization
_networkTime = new NetworkTime(_networkClient);

// Update loop
if (_networkTime.IsSynced)
{
    var serverTime = _networkTime.UtcNow;
    // Use synchronized time
}
```

## Best Practices

### Check IsSynced Before Use

Always verify sync status before using `UtcNow`. Show "Syncing..." UI or wait if not synced yet.

### Handle Disconnects

Dispose and recreate `NetworkTime` after reconnecting. Subscribe to `OnDisconnected` and `OnConnected` events to manage lifecycle.

### Adjust Sync Interval

Balance accuracy vs network traffic. Use shorter intervals (5 seconds) for competitive games requiring high precision, or longer intervals (1 minute) for turn-based games where slight drift is acceptable.

### Use for Relative Time

For countdowns and durations, calculate relative to server time:

```csharp
var turnDeadline = networkTime.UtcNow.AddSeconds(30);
var remaining = turnDeadline - networkTime.UtcNow;
if (remaining.TotalSeconds <= 0) { /* Turn expired */ }
```

## Limitations

The algorithm assumes symmetric network latency (upload ≈ download time). Asymmetric connections reduce accuracy, though for most games the error is negligible (< 50ms). System clocks drift over time, which periodic resyncing compensates for. Variable latency causes offset fluctuations; consider implementing exponential smoothing if needed.

## Key Points

- Provides server-synchronized UTC time using NTP-like algorithm with round-trip time calculation
- Automatic periodic sync every 30 seconds (configurable)
- Engine-agnostic - works with Godot, Unity, MonoGame, etc.
- Always check `IsSynced` before using `UtcNow`
- Subscribe to `OnTimeSynced` for reactive updates
- Call `Dispose()` when done to stop periodic syncing

## Next Steps

- [Network Actions](02-network-actions.md) — Send synchronized actions
- [Collective Actions](03-collective-actions.md) — Coordinate multi-player actions
