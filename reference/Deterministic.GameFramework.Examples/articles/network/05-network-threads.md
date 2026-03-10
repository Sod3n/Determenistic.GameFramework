# Network Threads

Network threads (also called channels) organize different types of messages into separate processing queues. This prevents low-priority messages like chat or ping/pong from blocking critical game actions.

## Why Separate Threads

In multiplayer games, different message types have different priorities and processing requirements. Without separation, a flood of chat messages could delay turn actions, or ping/pong heartbeats could interfere with gameplay synchronization.

Network threads solve this by routing messages to dedicated queues. Each thread processes independently, allowing chat spam to queue up without blocking the Main game thread.

## Built-in Threads

The framework provides three default threads:

- **Main** - Game actions, state changes, turn processing (lockable during animations)
- **PingPong** - Heartbeat messages for latency monitoring (always processes)
- **Chat** - Chat messages and social features (always processes)

Only the Main thread can be locked (e.g., during animations). Other threads always process immediately to maintain responsiveness.

## How It Works

When a `NetworkAction` arrives from the server, `NetworkClient` routes it to the appropriate queue based on the action's `Thread` property:

```csharp
public class ChatAction : NetworkAction<GameState>
{
    public override NetworkThread Thread => NetworkThread.Chat;
    // ...
}
```

The `ActionProcessor` processes each thread's queue independently every frame. If the Main thread is locked, its queue pauses while other threads continue processing.

See [NetworkThread.cs](../../TurnBasedPrototype.Server.Network/NetworkThread.cs), [NetworkClient.cs](../../TurnBasedPrototype.Shared.Network/NetworkClient.cs), and [ActionProcessor.cs](../../TurnBasedPrototype.Shared.Network/ActionProcessor.cs) for implementation details.

## Custom Threads

The registry pattern allows you to define custom threads without modifying core framework code. Register custom threads at application startup before creating `NetworkClient`:

```csharp
// Register custom threads
public static class CustomThreads
{
    public static readonly NetworkThread VoiceChat = NetworkThread.Register(100, "VoiceChat");
    public static readonly NetworkThread Analytics = NetworkThread.Register(101, "Analytics");
    public static readonly NetworkThread Replay = NetworkThread.Register(102, "Replay");
}

// Use in actions
public class VoiceChatAction : NetworkAction<GameState>
{
    public override NetworkThread Thread => CustomThreads.VoiceChat;
}
```

Thread IDs must be unique. Use ID ranges to organize your threads (e.g., 0-99 for built-in, 100-199 for gameplay, 200-299 for social features).

## Locking Threads

Lock the Main thread during animations or cutscenes to prevent actions from executing mid-animation:

```csharp
using (actionProcessor.Lock())
{
    // Play animation
    await PlayAttackAnimation();
}
// Lock released, Main thread resumes processing
```

Only the Main thread supports locking by default. Other threads always process to maintain system responsiveness (ping/pong, chat, etc.).

## Thread Routing

Actions specify their thread via the `Thread` property. The default is Main:

```csharp
public class AttackAction : NetworkAction<GameState>
{
    // Implicitly uses NetworkThread.Main
}

public class PingAction : NetworkAction<GameState>
{
    public override NetworkThread Thread => NetworkThread.PingPong;
}
```

If an action arrives with an unregistered thread ID, it defaults to Main thread for safety.

## Best Practices

### When to Create Custom Threads

Create custom threads when you need independent processing for specific message types. Good candidates include voice chat packets, analytics events, replay data, or any high-frequency messages that shouldn't block gameplay.

### Thread ID Organization

Use consistent ID ranges to avoid conflicts. Document your ID allocation scheme in a central location. Reserve ranges for future expansion.

### Registration Timing

Register all custom threads at application startup before creating `NetworkClient` or `ActionProcessor`. The framework automatically initializes queues and events for all registered threads.

### Thread vs Action Design

Don't create threads for every action type. Group related actions by processing requirements. For example, all chat-related actions (send message, typing indicator, read receipt) should use the Chat thread.

## Limitations

Custom threads cannot be locked by default. Only the Main thread has a locker. If you need lockable custom threads, you'll need to extend `ActionProcessor` to support additional lockers.

Thread registration is not synchronized across network. Both client and server must register the same custom threads with matching IDs. Consider using a shared constants file.

## Key Points

- Network threads separate messages into independent processing queues
- Built-in threads: Main (lockable), PingPong, Chat (always process)
- Registry pattern allows custom threads via `NetworkThread.Register(id, name)`
- Actions specify their thread via `Thread` property (defaults to Main)
- Lock Main thread during animations with `actionProcessor.Lock()`
- Register custom threads at startup before creating NetworkClient

## Next Steps

- [Network Actions](02-network-actions.md) — Create synchronized actions
- [Collective Actions](03-collective-actions.md) — Coordinate multi-player actions
