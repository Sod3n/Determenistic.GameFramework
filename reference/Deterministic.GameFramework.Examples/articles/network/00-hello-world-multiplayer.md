# Hello World - Multiplayer

Build a minimal working multiplayer game from scratch. This tutorial covers everything needed to get clients and server communicating.

## What You'll Build

A simple multiplayer counter where any player can increment a shared value. All players see the same counter in real-time.

## Prerequisites

- .NET 8.0 SDK
- Basic understanding of [DAR fundamentals](../getting-started/01-hello-world.md)

## Architecture Overview

```
CLIENT A                    SERVER                      CLIENT B
   │                           │                           │
   ├─ Connect ────────────────►│                           │
   │                           ├─ Create/Join Match        │
   │                           │◄────────────── Connect ───┤
   │                           │                           │
   ├─ IncrementAction ────────►│                           │
   │                           ├─ Validate & Execute       │
   │◄──────────────────────────┤───────────────────────────►│
   │  Broadcast action         │  Broadcast action         │
   └─ Update UI                │                 Update UI ─┘
```

## Step 1: Create the Game State

Your game state needs networking infrastructure:

```csharp
public class MultiplayerGameState : BaseGameState
{
    public DomainRegistry Registry { get; }
    public CounterDomain Counter { get; }
    
    public MultiplayerGameState(Guid matchId) : base(matchId, randomSeed: 0)
    {
        Registry = new DomainRegistry(this);
        new IdProviderDomain(this);  // Assigns sequential IDs
        Counter = new CounterDomain(this);
    }
}
```

`DomainRegistry` provides ID-based lookups. `IdProviderDomain` ensures deterministic IDs across all clients.

## Step 2: Create a Network Action

Extend `NetworkAction<T>` and mark serializable properties:

```csharp
public class IncrementAction : NetworkAction<MultiplayerGameState>
{
    [JsonProperty] public int Amount { get; set; } = 1;
    
    protected override void ExecuteProcess(MultiplayerGameState game)
    {
        game.Counter.Value += Amount;
    }
}
```

## Step 3: Set Up the Server

The server needs:
1. **ServerDomain** - Manages networking infrastructure
2. **ASP.NET Core** - Hosts the SignalR hub
3. **GameHub** - Handles client connections and action routing

```csharp
var server = new ServerDomain();
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
var app = builder.Build();
app.MapHub<GameHub>("/gamehub");
app.Run();
```

The framework's `GameHub` automatically handles match management and action broadcasting.

## Step 4: Create the Client

Clients need to:
1. **Connect** to the server via SignalR
2. **Join a match** to enter a game session
3. **Send actions** to the server
4. **Receive actions** from server and execute locally

```csharp
// Connect
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/gamehub")
    .Build();

// Handle incoming actions
connection.On<string>("SyncActions", actionsJson => {
    executor.ExecuteBatch(actionsJson);  // Execute locally
});

// Send action
var json = JsonConvert.SerializeObject(new IncrementAction());
await connection.InvokeAsync("SyncActions", json);
```

When the server broadcasts an action, all clients execute it locally to stay synchronized.

## Step 5: Run It

1. **Start the server** - Hosts the SignalR hub
2. **Run multiple clients** - Each connects to the same match
3. **Send actions** - Any client can increment the counter
4. **Observe synchronization** - All clients see the same value

When Client A increments, the server broadcasts to both clients. Both execute the action locally and see the updated counter value.

## What Just Happened?

1. **Server started** - Listening for SignalR connections
2. **Clients connected** - Established WebSocket connections
3. **Clients joined match** - Both connected to the same game session
4. **Action sent** - Client A sends `IncrementAction` to server
5. **Server executes** - Validates and runs action on server game state
6. **Server broadcasts** - Sends action to ALL clients in the match
7. **Clients execute** - Both clients run the same action locally
8. **State synchronized** - All clients and server have identical state

## Key Concepts

### Server Authority
The server validates and executes actions first. Clients trust the server's version of game state.

### Action Broadcasting
When one client sends an action, ALL clients (including the sender) receive and execute it. This keeps everyone synchronized.

### ID-Based References
Actions use integer IDs (`CounterId = 1`) instead of object references because objects can't be serialized over the network.

### Local Execution
Clients maintain their own game state and execute actions locally. This provides instant feedback and reduces network traffic.

### Automatic State Sync
When a client connects (or reconnects), `GameHub.OnClientConnected` automatically sends the full game state history to that client. This ensures:
- **New clients** start with the correct game state
- **Reconnecting clients** catch up on missed actions
- **No manual sync needed** - the framework handles it automatically

The sync uses `SyncGameStateAction` which replays all actions from the match history, guaranteeing deterministic state reconstruction.

## Common Issues

**Clients desync:**
- Ensure all clients use the same game logic
- Use deterministic IDs (IdProviderDomain)
- Avoid `Guid.NewGuid()` or `Random.Shared` in game logic

**Actions not received:**
- Check that clients are in the same match
- Verify SignalR connection is established
- Check server logs for errors

**Validation failures:**
- Override `_IsExecutable` in your action
- Check that referenced IDs exist in game state

## Real Implementation

See the **MultiplayerHelloWorld** project in `/Server/MultiplayerHelloWorld/` for a complete, runnable example with:

**What's Included:**
- Real ASP.NET Core server with SignalR
- Console client that connects over WebSockets
- Shared game state and actions
- Full synchronization between multiple clients

**How to Run:**

Terminal 1 - Start Server:
```bash
cd Server/MultiplayerHelloWorld/Server
dotnet run
```

Terminal 2 & 3 - Start Clients:
```bash
cd Server/MultiplayerHelloWorld/Client
dotnet run
```

Type `+5` in any client to increment the counter. All clients see the update in real-time!

See `MultiplayerHelloWorld/README.md` for detailed instructions and troubleshooting.

## Next Steps

- [Network Game State](01-network-game-state.md) — Add more networking components
- [Network Actions](02-network-actions.md) — Validation and server modes
- [Determinism](../advanced/02-determinism.md) — Ensure identical state across clients
