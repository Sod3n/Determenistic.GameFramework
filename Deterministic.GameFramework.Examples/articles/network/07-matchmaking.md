# Matchmaking

How the framework manages matches — creating, joining, tracking, and cleaning up game sessions on the server.

## Overview

Matchmaking in the DAR framework is built around three core components working together:

- **MatchManager** — Thread-safe registry of all active matches. Handles creation, lookup, and removal.
- **GameHub** — SignalR hub that bridges client connections to match management. Handles the full lifecycle: connect, join match, sync state, disconnect, and cleanup.
- **IGameStateFactory** — Creates game state instances for new matches. Decouples initialization logic from the match registry.

The flow is straightforward: a client connects to the `GameHub` with a `userId` and `matchId` as query parameters. If the match doesn't exist yet, `MatchManager` creates it via the factory. The client is added to a SignalR group for that match and receives the full game state history. When all clients disconnect, the match is automatically cleaned up.

## MatchManager

`MatchManager<TGameState>` is the central registry for all active matches on the server. Its public API exposes `CreateMatch`, `GetMatch`, `RemoveMatch`, `GetAllMatchIds`, and a `MatchCount` property — all thread-safe.

### Creating a Match

When `CreateMatch` is called, the factory produces a new game state, which is stored in an internal dictionary and then **scheduled** to be added to `ServerDomain.Subdomains` on the game loop thread. This scheduling is critical — it ensures the domain tree is only modified from the game loop, avoiding race conditions with action execution.

Once the match is added to `ServerDomain.Subdomains`, it becomes a live part of the domain tree. The `NetworkSyncManager` (a sibling subdomain of `ServerDomain`) automatically monitors all child game states and broadcasts their network actions to the correct SignalR groups. No additional wiring is needed.

An `OnMatchCreated` event fires after creation, which extensions like `DeterminismValidatingMatchManager` use to attach validation hooks.

### Removing a Match

`RemoveMatch` also schedules its work on the game loop: it removes the match from `ServerDomain.Subdomains` and calls `Dispose()`. This guarantees that any in-flight actions finish processing before the match is destroyed.

### Thread Safety

All `MatchManager` methods are protected by a lock. This is necessary because `GameHub` methods run on SignalR's thread pool (multiple connections simultaneously), while the game loop runs on its own thread. Match creation and removal must be atomic to prevent dictionary corruption.

## IGameStateFactory

The factory interface has a single method: `CreateGameState(Guid matchId)`. The `matchId` serves double duty — it identifies the match **and** seeds deterministic systems (random numbers, sequential IDs). Two matches created with the same `matchId` produce identical initial states, which is the foundation of deterministic replay.

For simple cases, `DefaultGameStateFactory` wraps a delegate — just pass a lambda like `matchId => new MyGameState(matchId, matchId.GetHashCode())`. For complex initialization that requires injected services or configuration, implement the interface directly. See `BattleGameStateFactory` in the example file for a custom factory that injects a `GameConfig` into each new game state.

## GameHub — Connection Lifecycle

`GameHub<TMatchManager, TGameState>` handles the full connection lifecycle. Clients connect with `userId` and `matchId` as query parameters on the SignalR URL.

### Connecting

When a client connects (`OnConnectedAsync`), the hub performs six steps in sequence:

1. **Parse parameters** — Extract `userId` and `matchId` from the HTTP query string
2. **Store connection** — Map `ConnectionId → (PlayerId, MatchId)` in a `ConcurrentDictionary` for later lookup
3. **Create or get match** — Call `MatchManager.GetMatch(matchId)`; if null, call `CreateMatch` to lazily create it
4. **Track player count** — Increment a per-match player counter (used for cleanup decisions)
5. **Join SignalR group** — Add the connection to the match's broadcast group so it receives action broadcasts
6. **Sync state** — Call `OnClientConnected`, which sends the full action history via `SyncGameStateAction`

This lazy creation model means the first client to connect with a given `matchId` creates the match. Subsequent clients with the same `matchId` join the existing match.

### State Sync on Join

After connecting, the server serializes the entire action history from `HistoryDomain` into a `SyncGameStateAction` and sends it to the newly connected client only. The client replays all actions to reconstruct the current game state deterministically. This handles new players, reconnecting players, and late joiners without any manual sync logic.

The `OnClientConnected` method is virtual — override it in a custom hub to add game-specific logic (assigning player slots, sending welcome messages, notifying other players).

### Disconnecting

When a client disconnects (`OnDisconnectedAsync`), the hub removes the connection mapping, leaves the SignalR group, and decrements the player count. If no players remain, the match is removed after a **2-second delay**. This delay allows any pending actions already scheduled on the game loop to finish executing before the match is destroyed, preventing edge cases where a disconnect races with an in-flight action.

## Server Setup

The `AddMultiplayerServer` extension method on `IServiceCollection` registers everything needed in one call: `ServerDomain` (singleton), the `IGameStateFactory`, `MatchManager` (singleton), and SignalR services. After building the app, map either `DefaultGameHub` (zero-config) or a custom hub to a SignalR endpoint.

```csharp
// Minimal setup — delegate factory + default hub
builder.Services.AddMultiplayerServer<MyGameState>(
    matchId => new MyGameState(matchId, randomSeed: matchId.GetHashCode())
);
app.MapHub<DefaultGameHub<MyGameState>>("/gamehub");
```

For custom factory or hub implementations, see `MinimalServerSetup` and `CustomServerSetup` in the example file.

## Match ID Strategy

The `matchId` is a `Guid` provided by the client at connection time. The framework is intentionally agnostic about how matches are formed — it only cares that both players connect with the same `matchId`. This gives you full flexibility:

- **Shared room code** — One player generates a `matchId` and shares it with others via a lobby, invite link, or chat. The other player connects with the same ID. Simple and works well for friend-to-friend games.

- **External matchmaker** — A separate matchmaking service (your own API, PlayFab, etc.) pairs players based on skill, region, or queue and assigns them a shared `matchId`. Both players are notified and connect independently. The framework creates the match when the first player arrives.

- **Deterministic match ID** — Derive the `matchId` from inputs (player IDs + timestamp) using a hash. The same inputs always produce the same `matchId`, which means the same random seed. Useful for ranked/seeded matches and deterministic replay verification.

See the `MatchIdStrategies` section in the example file for concrete implementations of each approach.

## DeterminismValidatingMatchManager

For development and testing, `DeterminismValidatingMatchManager` wraps `MatchManager` to create a **shadow game state** for each match. It hooks into the `OnMatchCreated` event and creates a second, isolated game state with the same `matchId` (and therefore the same random seed). Every action executed on the primary state is also executed on the shadow, and the results are compared field-by-field to detect determinism violations.

This is a development-only tool — it doubles memory and CPU usage per match. Enable it to catch non-deterministic code (e.g., `DateTime.Now`, `Random.Shared`, dictionary iteration order) early in development. See [Determinism](../advanced/02-determinism.md) for details on what causes desync and how to avoid it.

## Architecture Summary

The server architecture has a clear hierarchy:

- **ServerDomain** is the root. It owns the `GameLoop` (60 fps tick) and `NetworkSyncManager` (20Hz broadcast).
- **Matches** are subdomains of `ServerDomain`. Each match is an independent game state tree.
- **MatchManager** is the CRUD interface — it adds/removes matches from the `ServerDomain` subdomain tree.
- **GameHub** bridges SignalR connections to `MatchManager`. It creates matches on first connect, routes actions, and cleans up empty matches.
- **NetworkSyncManager** collects all network actions marked with `SyncToClient = true` from any match, groups them by `matchId`, and broadcasts them to the correct SignalR group at 20Hz.
- **IGameStateFactory** creates isolated game state instances. Each match gets its own tree with its own `MatchIdDomain`, `DomainRegistry`, `HistoryDomain`, etc.

## Key Takeaways

- **Matches are subdomains** of `ServerDomain` — adding/removing them is a domain tree operation
- **Thread safety** is handled by `MatchManager`'s lock and `GameLoop.Schedule()`
- **Match creation is lazy** — the first client to connect with a `matchId` creates the match
- **Cleanup is automatic** — empty matches are removed after a short delay
- **State sync is built-in** — new clients receive full history on connect
- **The factory pattern** decouples game state creation from match management
- **Match ID = random seed** — same `matchId` produces identical initial states for determinism

## Real Implementation

See `Examples/Network/Example09_Matchmaking.cs` for runnable code covering:
- Custom `IGameStateFactory` with config injection
- Custom `GameHub` with connection hooks
- All three match ID strategies (room code, external matchmaker, deterministic)
- Minimal and custom server setup
- Client connection example

## Next Steps

- [Hello World - Multiplayer](00-hello-world-multiplayer.md) — Build a working multiplayer game
- [Network Game State](01-network-game-state.md) — Components that power networked game states
- [Determinism](../advanced/02-determinism.md) — Ensure identical state across clients
