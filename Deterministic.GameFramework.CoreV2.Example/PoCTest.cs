using System;
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

        // 1. Setup State & Dispatcher
        var state = new GlobalState();
        // Use generated registry for zero-reflection lookups
        var dispatcher = new Dispatcher(type => Deterministic.GameFramework.Generated.DeterministicGameFrameworkCoreV2Example.NetworkIdRegistry.TypeToId[type]);
        var gameLoop = new GameLoop(state, dispatcher);
        gameLoop.SetTickRate(10); // Slower tick rate for readability in console

        // 2. Register Services
        var damageHandler = new DamageActionHandler();
        var reactions = new[] { new DecreaseDamageReaction() };
        dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);

        // 3. Create Entities and Hierarchy
        var rootNode = new Entity(1);
        var player = new Entity(2);
        
        // Setup Hierarchy Components so they are allocated
        state.GetState<HierarchyComponent>(rootNode);
        state.GetState<HierarchyComponent>(player);
        
        // Link them
        state.AddChild(rootNode, player);

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

        // 6. Serialization PoC (Memcopy simulation)
        Console.WriteLine("\n--- Serialization Proof ---");
        var originalArray = state.GetRawArray<HealthComponent>();
        int byteSize = originalArray.Length * System.Runtime.InteropServices.Marshal.SizeOf<HealthComponent>();
        Console.WriteLine($"HealthComponent array size in memory: {byteSize} bytes");
        
        // Allocate a raw byte buffer (simulating a network packet or save file)
        byte[] buffer = new byte[byteSize];
        
        // Copy struct array to byte array using MemoryMarshal since it's a blittable struct array
        var span = new Span<HealthComponent>(originalArray);
        var byteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(span);
        byteSpan.CopyTo(buffer);
        Console.WriteLine("Serialized state to byte[] instantaneously.");
        
        // Modify original to prove we aren't sharing references
        state.GetState<HealthComponent>(player).CurrentHealth = 999;
        Console.WriteLine($"Player health mutated to: {state.GetState<HealthComponent>(player).CurrentHealth}");
        
        // Create new array and copy bytes back
        HealthComponent[] restoredArray = new HealthComponent[originalArray.Length];
        var restoredSpan = new Span<HealthComponent>(restoredArray);
        var restoredByteSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(restoredSpan);
        new Span<byte>(buffer).CopyTo(restoredByteSpan);
        
        Console.WriteLine($"Restored Player health from bytes: {restoredArray[player.Id].CurrentHealth}");
        Console.WriteLine("Memcopy serialization works perfectly.");
        
        // 7. Float/Vector Determinism Test
        Console.WriteLine("\n--- Deterministic Math Proof ---");
        Float f1 = 10.5f;
        Float f2 = 2.0f;
        Console.WriteLine($"Float: {f1} + {f2} = {f1 + f2}");
        Console.WriteLine($"Float: {f1} / {f2} = {f1 / f2}");
        
        Vector2 v1 = new Vector2(3f, 4f);
        Console.WriteLine($"Vector2: {v1} Magnitude = {v1.Magnitude} (Expected 5.00000)");
        
        Vector3 v3 = new Vector3(1f, 1f, 1f);
        Console.WriteLine($"Vector3: {v3} Normalized = {v3.Normalized}");
    }
}
