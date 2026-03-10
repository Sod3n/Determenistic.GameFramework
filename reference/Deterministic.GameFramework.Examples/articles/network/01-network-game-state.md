# Network Game State

Multiplayer games require additional infrastructure for synchronization, determinism, and action routing. The framework provides composable domain components you can add to your game state.

## Core Components

These domains add multiplayer capabilities:

- **MatchIdDomain** - Identifies the match, auto-tags all network actions
- **DomainRegistry** - Fast ID → Domain lookup for action targeting
- **IdProviderDomain** - Assigns sequential IDs (deterministic, network-safe)
- **HistoryDomain** - Records all network actions for replay/debugging
- **RandomProviderDomain** - Provides deterministic random seeds

Each component is a domain that uses reactions to add functionality to your game state.

## MatchIdDomain

Automatically tags all network actions with the match ID using a `Prepare` reaction:

```csharp
new Reaction<LeafDomain, INetworkAction>(parent)
    .Prepare((_, action) => action.MatchId = MatchId)
```

This ensures every network action knows which match it belongs to, enabling the server to route actions correctly.

## DomainRegistry

Provides fast O(1) lookup of domains by ID using a cached dictionary. This is essential for network actions that reference entities by ID rather than object references.

```csharp
public LeafDomain? GetDomain(int id) => _registry.TryGetValue(id, out var d) ? d : null;
```

The registry automatically updates as domains are added/removed from the tree.

## IdProviderDomain

Assigns sequential IDs (1, 2, 3...) to domains as they're added to the tree. This ensures deterministic, network-safe IDs instead of GUIDs.

```csharp
new Reaction<LeafDomain, AddSubdomainAction>(target)
    .After((_, action) => action.Child.Id = _counter++)
```

Same actions executed in the same order produce the same IDs on all clients.

## HistoryDomain

Records all network actions in a list. Useful for:
- **Automatic state sync** - New/reconnecting clients receive full history
- Replaying matches
- Debugging desync issues
- Implementing undo/redo
- Spectator mode (replay from start)

Uses an `After` reaction to capture actions after they execute successfully.

**Connection/Reconnection:** When a client connects, `GameHub.OnClientConnected` automatically sends the entire history to that client via `SyncGameStateAction`. The client replays all actions to reconstruct the current game state deterministically. This means:
- No need to manually sync state
- Reconnecting clients automatically catch up
- Late joiners can enter mid-game (if your game logic allows it)

## RandomProviderDomain

Provides deterministic random numbers by injecting seeds into actions that implement `IRequireRandom`. All clients use the same initial seed, so they generate the same random sequence.

See the [Determinism article](../advanced/02-determinism.md) for details on deterministic randomness.

## Composing Your Game State

You can add components individually:

```csharp
public class MyGameState : BranchDomain
{
    public MyGameState() : base(null)
    {
        new DomainRegistry(this);
        new IdProviderDomain(this);
        // Add only what you need
    }
}
```

Or use the convenience `NetworkGameState` base class that includes all components:

```csharp
public class MyGameState : NetworkGameState
{
    public MyGameState(Guid matchId, int seed) : base(matchId, seed) { }
}
```

## Key Takeaways

These components are **domains that use reactions** to add functionality. This demonstrates the power of the DAR pattern - complex features can be added as composable, reusable domains without modifying your core game logic.

Each component is optional. Add only what your game needs.
