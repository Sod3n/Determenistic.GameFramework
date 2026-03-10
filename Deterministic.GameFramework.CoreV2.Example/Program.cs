using System;
using System.Threading;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
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
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<PhysicsWorldState>();

        // 3. Register Systems
        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.SystemRunner.EnableSystem(physicsSystem);

        // 4. Create Entity
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D { GlobalPosition = new Vector2(0, 10), GlobalRotation = 0, GlobalScale = Vector2.One });
        // RigidBody2D contains Velocity
        state.AddComponent(entity, new RigidBody2D { 
            Mass = 1, 
            LinearVelocity = new Vector2(1, 0),
            GravityScale = 1
        });
        state.AddComponent(entity, CollisionShape2D.CreateCircle((float)0.5));

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
                var pos = state.GetComponent<Transform2D>(entity).GlobalPosition;
                Console.WriteLine($"Tick {gameLoop.CurrentTick}: Entity Pos: {pos}");
            }
        }

        gameLoop.Stop();
        physicsSystem.Dispose();
        Console.WriteLine("Simulation finished.");
    }
}
