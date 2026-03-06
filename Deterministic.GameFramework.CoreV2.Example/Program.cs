using System;
using System.Threading;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.Physics.Components;
using Deterministic.GameFramework.Physics.Systems;

namespace Deterministic.GameFramework.CoreV2.Example;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Initializing Game Framework with Rapier Physics...");

        // 1. Setup Core
        var state = new GlobalState();
        var dispatcher = new Dispatcher();
        var scheduler = new ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);

        // 2. Register Components
        state.RegisterComponent<Position>();
        state.RegisterComponent<Velocity>();
        state.RegisterComponent<PhysicsBody2D>();
        state.RegisterComponent<PhysicsWorldState>();

        // 3. Register Systems
        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.RegisterSystem(physicsSystem);

        // 4. Create Entity
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Position { Value = new Vector2(0, 10) });
        state.AddComponent(entity, new Velocity { Value = new Vector2(1, 0) }); // Move right
        state.AddComponent(entity, new PhysicsBody2D { Mass = 1, IsStatic = false });

        Console.WriteLine($"Entity {entity.Id} Created at (0, 10) with Velocity (1, 0)");

        // 5. Run Loop manually for a few frames
        gameLoop.Start();

        // Simulate for 1 second
        int ticksToRun = 60;
        for (int i = 0; i < ticksToRun; i++)
        {
            Thread.Sleep(16);
            
            if (i % 10 == 0)
            {
                var pos = state.GetComponent<Position>(entity).Value;
                Console.WriteLine($"Tick {gameLoop.CurrentTick}: Entity Pos: {pos}");
            }
        }

        gameLoop.Stop();
        physicsSystem.Dispose();
        Console.WriteLine("Simulation finished.");
    }
}
