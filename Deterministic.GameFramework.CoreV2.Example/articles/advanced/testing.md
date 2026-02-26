# Testing & Debugging

One of the biggest benefits of a deterministic framework is that **testing is incredibly reliable**. If a test passes once, it will pass forever. You don't have to worry about race conditions or random timing failures in your game logic.

## Unit Testing Game Logic

Because your gameplay logic is separated into pure **Actions** and **ActionServices**, you can unit test them without spinning up a full game server or Unity scene.

### Example: Testing a Heal Potion

```csharp
[Fact]
public void TestHealPotion()
{
    // 1. Setup a clean state
    var state = new GlobalState();
    var entity = state.CreateEntity();
    
    state.AddComponent(entity, new Health { Current = 10, Max = 100 });
    
    // 2. Setup the service
    var service = new HealService();
    var context = new Context(state, entity);
    
    // 3. Execute the action manually
    var action = new HealAction { Amount = 50 };
    ref var health = ref state.GetState<Health>(entity);
    
    // We can call the internal execute method directly for testing
    // Or go through a Dispatcher if we want to test Reactions too
    service.ExecuteTest(action, ref health, context);
    
    // 4. Assert
    Assert.Equal(60, health.Current);
}
```

## Integration Testing (The Game Loop)

You can run a `GameLoop` manually step-by-step. This is perfect for testing interactions over time, like buffs, cooldowns, or movement interpolation.

```csharp
public void TestRegenBuff()
{
    // Setup
    var state = new GlobalState();
    var loop = new GameLoop(state, dispatcher, scheduler);
    var player = state.CreateEntity();
    
    // Apply Buff
    loop.Schedule(new ApplyRegenBuff(), player);
    
    // Run 60 ticks (1 second)
    for(int i=0; i<60; i++)
    {
        loop.RunSingleTick();
    }
    
    // Verify health increased
    var health = state.GetState<Health>(player);
    Assert.True(health.Current > 10);
}
```

## Debugging Desyncs

A "Desync" happens when the Client predicts one thing, but the Server says something else happened.
In the framework, this manifests as a **Rollback** that results in a different state than the prediction.

### Common Causes
1. **Float Math**: Using `float` instead of `Float`. (Use the Analyzer!)
2. **Unordered Iteration**: Iterating over a `Dictionary` or `HashSet` in gameplay logic.
3. **Local State**: Relying on a variable *outside* of `GlobalState` (like a static variable or a field in a class).
4. **Randomness**: Using `System.Random` instead of `DeterministicRandom`.

### How to Debug
1. **Log the Rollback**: The `GameLoop` prints when a rollback occurs.
   ```
   [Rollback] Rolling back from 100 to 95
   ```
2. **Compare Snapshots**:
   If you have a replay file (or logs), compare the state hash at Tick 95 on the Client vs the Server.
   
3. **Isolate the Action**:
   Identify which Action triggered the divergence. Was it a specific spell? A collision?
   Write a unit test that repeats that specific sequence of inputs.

## Visual Debugging

Since `GlobalState` is just a collection of arrays, it is easy to visualize.
- **State Inspector**: You can write a simple tool to reflect over `_componentArrays` and show values in a UI.
- **History Viewer**: Since `GameLoop` keeps a `StateHistory` buffer, you can pause the game and "scrub" backward in time to see exactly what happened 10 frames ago.
