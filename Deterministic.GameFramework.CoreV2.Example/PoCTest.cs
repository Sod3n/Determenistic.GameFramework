using System;
using System.Runtime.InteropServices;
using System.Threading;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Example.Reactions;
using Deterministic.GameFramework.CoreV2.Example.Services;

namespace Deterministic.GameFramework.CoreV2.Example;

public static class PoCTest
{
    public static void Run()
    {
        Console.WriteLine("Starting V2 Core Proof of Concept Test...");

        // 1. Setup State & Dispatcher & Scheduler
        var state = new GlobalState();
        var scheduler = new ActionScheduler();
        // Use generated registry for zero-reflection lookups
        var dispatcher = new Dispatcher(type => Deterministic.GameFramework.Generated.DeterministicGameFrameworkCoreV2Example.NetworkIdRegistry.TypeToId[type]);
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(10); // Slower tick rate for readability in console

        // 2. Register Services
        var damageHandler = new DamageActionHandler();
        var reactions = new[] { new DecreaseDamageReaction() };
        dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);
        
        // Register Hierarchy Reaction
        dispatcher.RegisterReaction(new RegionDamageReaction());

        // 3. Create Entities and Hierarchy
        var rootNode = new Entity(1);
        var player = new Entity(2);
        
        // Setup Hierarchy Components so they are allocated
        state.GetState<HierarchyComponent>(rootNode);
        state.GetState<HierarchyComponent>(player);
        
        // Add RegionComponent to root
        state.AddComponent(rootNode, new RegionComponent { DamageCounter = 0 });
        // Add Reaction Tag to root to subscribe to the reaction using the new helper
        rootNode.AddReaction<RegionDamageReactionTag>(state);
        
        // Link them
        rootNode.AddChild(player, state);

        // Initialize State
        ref var health = ref state.GetState<HealthComponent>(player);
        health.CurrentHealth = 100;

        Console.WriteLine($"Player initial health: {state.GetState<HealthComponent>(player).CurrentHealth}");
        Console.WriteLine($"Player parent ID: {state.GetState<HierarchyComponent>(player).ParentId}");

        // 4. Schedule Actions via GameLoop
        Console.WriteLine("\n--- GameLoop Execution ---");
        
        // Schedule immediate damage on next tick
        gameLoop.Schedule(new DamageAction(15), player);
        
        // Schedule future damage on tick 3
        gameLoop.ScheduleOnTick(3, new DamageAction(25), player);
        
        // Hook into tick for logging
        gameLoop.OnTick += () => 
        {
            if (gameLoop.CurrentTick <= 5)
            {
                var hp = state.GetState<HealthComponent>(player).CurrentHealth;
                Console.WriteLine($"[Tick {gameLoop.CurrentTick}] Player Health: {hp}");
            }
            if (gameLoop.CurrentTick == 5)
            {
                gameLoop.Stop();
            }
        };

        Console.WriteLine("Starting GameLoop for 5 ticks...");
        var loopTask = gameLoop.Start();
        loopTask.Wait(); // Block until gameLoop finishes (at tick 5)

        // 5. Automatic Rollback Test
        Console.WriteLine("\n--- Automatic Rollback Proof ---");
        
        // Setup: Reset Player Health
        state.GetState<HealthComponent>(player).CurrentHealth = 100;
        
        // Run until Tick 10
        Console.WriteLine("Simulating normal gameplay to Tick 10...");
        while (gameLoop.CurrentTick < 10)
        {
            // Use public manual tick
            gameLoop.RunSingleTick();
        }
        
        Console.WriteLine($"Current Tick: {gameLoop.CurrentTick}. Player Health: {state.GetState<HealthComponent>(player).CurrentHealth}");
        
        // Inject LATE packet for Tick 5 (We are at 10)
        Console.WriteLine("Injecting LATE packet for Tick 5...");
        var lateDamage = new DamageAction(10);
        int dSize = Marshal.SizeOf<DamageAction>();
        byte[] dBytes = new byte[dSize];
        MemoryMarshal.Write(dBytes, in lateDamage);
        
        scheduler.ScheduleFromBytes(1, dBytes, player.Id, 5);
        
        // Run Tick 11 - This should trigger Rollback!
        Console.WriteLine("Running Tick 11 (Should Rollback)...");
        gameLoop.RunSingleTick();
        
        // Verify
        // Start 100.
        // Tick 0: -15 (Scheduled in line 52) -> 85
        // Tick 3: -25 (Scheduled in line 55) -> 60
        // Tick 5: -10 (Late Packet) -> 50
        // Result should be 50.
        
        var healthAfterRollback = state.GetState<HealthComponent>(player).CurrentHealth;
        Console.WriteLine($"Player Health After Rollback: {healthAfterRollback} (Expected 50)");
        
        if (healthAfterRollback == 50)
        {
            Console.WriteLine("SUCCESS: Rollback & Resimulation Works!");
        }
        else
        {
            Console.WriteLine($"FAILURE: Expected 50, got {healthAfterRollback}. (Did it ignore the late packet?)");
        }
        
        // 5b. Verify Hierarchy Reaction
        var regionDamage = state.GetState<RegionComponent>(rootNode).DamageCounter;
        Console.WriteLine($"Region Damage Counter: {regionDamage}");
        // Expected: 
        // Tick 0: 15
        // Tick 3: 25
        // Tick 5: 10 (Late)
        // Total: 50
        if (regionDamage == 50)
        {
             Console.WriteLine("SUCCESS: Hierarchy Reaction Bubbling Works!");
        }
        else
        {
             Console.WriteLine($"FAILURE: Hierarchy Reaction Failed. Expected 50, got {regionDamage}");
        }

        // 6. Deterministic Math Verification
        Console.WriteLine("\n--- Deterministic Math Verification ---");
        Float f1 = new Float(10);
        Float f2 = new Float(3);
        Float f3 = f1 / f2; // 3.33333...
        Console.WriteLine($"10 / 3 = {f3}");
        
        Vector2 v1 = new Vector2(1, 1);
        Console.WriteLine($"Vector2(1, 1) Normalized = {v1.Normalized}");
        Console.WriteLine($"Vector2(1, 1) Magnitude = {v1.Magnitude}");
        
        Float fSqrt = Float.Sqrt(new Float(2));
        Console.WriteLine($"Sqrt(2) = {fSqrt}");

        if (Float.Abs(fSqrt * fSqrt - new Float(2)) < new Float(0.001f))
        {
             Console.WriteLine("SUCCESS: Math seems deterministic and accurate enough.");
        }
        else
        {
             Console.WriteLine("FAILURE: Math precision issues.");
        }

        // 7. Deterministic Random Verification
        Console.WriteLine("\n--- Deterministic Random Verification ---");
        var rng1 = new DeterministicRandom(12345);
        var rng2 = new DeterministicRandom(12345);
        
        Console.WriteLine($"RNG1 Next: {rng1.Next()}");
        Console.WriteLine($"RNG2 Next: {rng2.Next()}");
        
        if (rng1.Next() == rng2.Next())
        {
            Console.WriteLine("SUCCESS: RNG is deterministic (Same seed = Same sequence).");
        }
        else
        {
            Console.WriteLine("FAILURE: RNG is NOT deterministic.");
        }
        
        Float randFloat = rng1.NextFloat();
        Console.WriteLine($"Random Float [0,1): {randFloat}");

        // 8. FixedString32 & List8 Verification
        Console.WriteLine("\n--- Fixed Types Verification ---");
        var fs = new FixedString32("Hello World");
        Console.WriteLine($"FixedString32: '{fs}' (Len: {fs.ToString().Length})");
        
        var list = new List8<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);
        Console.WriteLine($"List8 Count: {list.Count}");
        Console.WriteLine($"List8 Items: {list[0]}, {list[1]}, {list[2]}");
        
        if (fs.ToString() == "Hello World" && list.Count == 3 && list[2] == 30)
        {
             Console.WriteLine("SUCCESS: Fixed Types work as expected.");
        }
        else
        {
             Console.WriteLine("FAILURE: Fixed Types verification failed.");
        }

        // 9. Deterministic Trig Verification
        Console.WriteLine("\n--- Deterministic Trig Verification ---");
        Float pi = Float.Pi;
        Float sin0 = Float.Sin(0);
        Float sinPi2 = Float.Sin(pi / 2);
        Float cosPi = Float.Cos(pi);
        Float atan1 = Float.Atan(1); // Should be Pi/4
        
        Console.WriteLine($"Sin(0) = {sin0} (Expected 0)");
        Console.WriteLine($"Sin(Pi/2) = {sinPi2} (Expected ~1)");
        Console.WriteLine($"Cos(Pi) = {cosPi} (Expected ~-1)");
        Console.WriteLine($"Atan(1) = {atan1} (Expected ~0.785)");

        if (Float.Abs(sin0) < new Float(0.01f) && 
            Float.Abs(sinPi2 - 1) < new Float(0.01f) &&
            Float.Abs(cosPi + 1) < new Float(0.01f))
        {
             Console.WriteLine("SUCCESS: Trig seems reasonable.");
        }
        else
        {
             Console.WriteLine("FAILURE: Trig precision issues.");
        }
    }
}
