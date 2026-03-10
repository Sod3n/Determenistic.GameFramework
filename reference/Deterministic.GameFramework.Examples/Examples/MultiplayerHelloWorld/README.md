# Multiplayer Hello World

A minimal, working multiplayer game using the DAR framework. Multiple clients connect to a server and share a synchronized counter.

**What it demonstrates:**
- Real ASP.NET Core server with SignalR
- Real clients connecting over WebSockets
- Synchronized game state across all clients
- Production-ready architecture matching the Godot client

## Quick Start

**Terminal 1 - Start Server:**
```bash
cd Server
dotnet run
```

**Terminal 2 & 3 - Start Clients:**
```bash
cd Client
dotnet run
```

**Play:**
- Type `+5` to increment counter by 5
- Type `+10` to increment by 10
- Type `q` to quit

All clients see the same counter value in real-time!

## Code Overview

**Server Setup (Program.cs):**
```csharp
// One line to set up all multiplayer infrastructure
builder.Services.AddMultiplayerServer<CounterGameState>(matchId => new CounterGameState(matchId));

// Use the default GameHub - no custom class needed
app.MapHub<DefaultGameHub<CounterGameState>>("/gamehub");
```

**Client Setup:**
```csharp
// Create client and connect
var client = new ClientDomain(userId, matchId);
var connection = await client.ConnectToServer("http://localhost:5000/gamehub");

// Send actions
client.Send(new IncrementAction { Amount = 5 });
```

**Game State:**
```csharp
public class CounterGameState : NetworkGameState
{
    public CounterDomain Counter { get; }
    // NetworkGameState provides: Registry, IdProvider, History, Random, MatchId
}
```

## Architecture

All networking infrastructure is provided by the framework:
- **`ClientDomain<TGameState>`** - Client root with GameLoop, NetworkSyncManager, Send()
- **`ServerDomain`** - Server root with GameLoop, NetworkSyncManager
- **`DefaultGameHub<TGameState>`** - SignalR hub (auto-creates matches, syncs state)
- **`MatchManager<TGameState>`** - Match lifecycle management
- **`NetworkGameState`** - Base class bundling common network components
- **`SendAction`** - Generic action for network transmission

Same architecture as the Godot client - only difference is SignalR connection management.

## Next Steps

See the documentation for:
- **Network Actions** - Validation, server modes, action injection
- **Determinism** - Ensuring identical state across clients
- **Network Game State** - Understanding the bundled components

The networking infrastructure is production-ready. Just add your game logic!

## Troubleshooting

- **Connection refused**: Start server first on `http://localhost:5000`
- **Clients desync**: Ensure same game logic, avoid `Guid.NewGuid()` or `Random.Shared`
- **Actions not received**: Check server console for errors
