# Networking & Rollback

The core superpower of **Deterministic.GameFramework.CoreV2** is its ability to handle **Rollback Networking** transparently. This allows for responsive gameplay even with network latency.

## The Game Loop

The `GameLoop` class is the heartbeat of the simulation. It runs at a fixed tick rate (e.g., 60 ticks per second).

```csharp
// Configure loop
var loop = new GameLoop(state, dispatcher, scheduler);
loop.SetTickRate(60);

// Start simulation
await loop.Start();
```

Each tick, the loop performs these steps:
1. **Check for Rollback**: Do we have new inputs for a past tick?
2. **Execute Actions**: Run all actions scheduled for the current tick.
3. **Save State**: Store a snapshot of the current state in history.
4. **Advance**: Increment `CurrentTick`.

## How Rollback Works

In a deterministic game, if we know the state at Tick 100, and we apply the exact same inputs, we will always get the same state at Tick 101.

### 1. Prediction (The "Happy Path")
The client simulates the game immediately when the user presses a button. It assumes its inputs will be accepted by the server. This provides **zero-latency feedback** to the player.

### 2. Reconciliation (The "Oh No" Moment)
Sometimes, the server sends an input from *another* player that happened in the past (e.g., at Tick 95), but our client is already at Tick 100.

Because the other player's input changes the outcome of the game (maybe they stunned us?), our simulation from Tick 95 to 100 is now **wrong**.

### 3. The Rollback Process
When the framework detects this "past input":
1. **Restore**: It looks up the saved state for Tick 94 (the tick *before* the new input).
2. **Resimulate**: It fast-forwards from Tick 95 to 100, reapplying all known inputs (including the new one).
3. **Continue**: The game continues from the corrected Tick 100.

This happens in a single frame, often undetectable to the user.

## Action Scheduling

To support this, actions are not executed immediately. They are **scheduled** for a specific tick.

```csharp
// Schedule an action for the CURRENT tick (Prediction)
loop.Schedule(new JumpAction(), playerEntity);

// Schedule an action for a FUTURE tick
loop.ScheduleOnTick(currentTick + 5, new SpawnEnemyAction(), enemyEntity);
```

The `ActionScheduler` manages these queues and handles the complexity of inserting actions into the past.
