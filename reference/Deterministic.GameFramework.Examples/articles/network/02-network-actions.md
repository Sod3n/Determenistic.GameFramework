# Network Actions

Network actions enable multiplayer by synchronizing game state across server and clients. The server executes actions authoritatively, then broadcasts them to all clients.

## The Flow

Network actions follow this pattern:

1. **Client sends** action JSON to server
2. **Server validates** - checks if action is legal
3. **Server executes** - authoritative state change
4. **Server broadcasts** - sends action to ALL clients
5. **Clients execute** - same action runs on all clients

This ensures the server is the source of truth while clients maintain synchronized local state.

## Creating Network Actions

Extend `NetworkAction<TGameState>` instead of `DARAction`:

```csharp
public class AttackAction : NetworkAction<MyGameState>
{
    [JsonProperty] public int TargetId { get; set; }
    [JsonProperty] public int Damage { get; set; }
    
    protected override void ExecuteProcess(MyGameState game)
    {
        var target = game.GetDomain(TargetId) as EnemyDomain;
        target.Health -= Damage;
    }
}
```

Use `[JsonProperty]` for data that needs to be sent over the network. Use `[JsonIgnore]` for runtime-only data.

## Why IDs Instead of References

Network actions use IDs to reference entities because object references can't be serialized:

```csharp
// ❌ Can't serialize
public EnemyDomain Target { get; set; }

// ✅ Serialize ID, resolve at execution
[JsonProperty] public int TargetId { get; set; }
```

The action resolves IDs to domains at execution time using `gameState.GetDomain(id)`.

## Validation

Override `_IsExecutable` to validate actions before execution:

```csharp
protected override bool _IsExecutable(MyGameState game)
{
    var target = game.GetDomain(TargetId);
    return target != null && Damage > 0 && Damage <= 100;
}
```

If validation fails, the action is rejected and `ExecuteProcess` never runs. This prevents cheating and invalid state.

## Action Injection

Network actions often need access to game state. Use the [Action Injection pattern](../advanced/01-action-injection.md) to inject dependencies:

```csharp
public interface IRequireGame : IDARAction
{
    MyGameState Game { get; set; }
}
```

The game state injects itself into actions via a `Prepare` reaction. See the Action Injection article for details.

## Client-Server Communication

Clients send actions to the server via SignalR:

```csharp
var json = JsonConvert.SerializeObject(action);
await hubConnection.InvokeAsync("SyncActions", json);
```

The server's `GameHub` receives, validates, executes, and broadcasts the action to all clients in the match.

## Server Modes

The server can operate in two modes:

### Normal Mode (Default)

The server simulates the full game state:
1. Receives action from client
2. Validates action against server game state
3. Executes action on server
4. Broadcasts action to all clients

This provides **server authority** - the server is the source of truth and can prevent cheating.

### Relay-Only Mode

The server acts as a simple relay without simulation:
1. Receives action from client
2. Immediately broadcasts to all clients (no validation)
3. No server-side game state

```csharp
ServerDomain.Instance.RelayOnlyMode = true;
```

**Use relay-only mode when:**
- You trust all clients (local network, co-op games)
- Server resources are limited
- Game logic is too complex for server simulation
- You want peer-to-peer-like behavior with centralized routing

**Trade-offs:**
- ✅ Lower server CPU/memory usage
- ✅ Simpler server deployment
- ❌ No cheat prevention
- ❌ No server-side validation
- ❌ Clients can desync if they have different game logic

## Key Points

- Network actions use **IDs** instead of object references
- Server is **authoritative** - validates and executes first
- Same action **executes on all clients** for synchronization
- Use `[JsonProperty]` for serialized data, `[JsonIgnore]` for runtime data
- Validation in `_IsExecutable` prevents cheating

## Next Steps

- [Collective Actions](03-collective-actions.md) — Coordinate multi-player actions
- [Determinism](../advanced/02-determinism.md) — Ensure identical state across clients

See **Example 5** for a complete network action implementation.
