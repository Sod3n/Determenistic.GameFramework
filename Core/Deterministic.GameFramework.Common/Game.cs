using System;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

/// <summary>
/// Central entry point for the deterministic game simulation.
/// Encapsulates the core systems: State, Loop, Dispatcher, Scheduler, and SceneManager.
/// </summary>
public class Game : IDisposable
{
    public EntityWorld State { get; }
    public GameLoop Loop { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    public SceneManager SceneManager { get; }
    public GameSimulation Simulation { get; }
    public GameLoopActionDispatcher GameLoopActionDispatcher { get; }
    

    public Game(EntityWorld? state = null, int tickRate = 60)
    {
        State = state ?? new EntityWorld();
        Scheduler = new ActionScheduler();
        Dispatcher = new Dispatcher();
        Simulation = new GameSimulation(State, Dispatcher, Scheduler);
        GameLoopActionDispatcher = new GameLoopActionDispatcher(Simulation);
        Dispatcher.ActionDispatcher = GameLoopActionDispatcher;
        Loop = new GameLoop(Simulation);
        Loop.SetTickRate(tickRate);
        
        Dispatcher.RegisterServices(
            ServiceLocator.GetAll<IActionService>(), 
            ServiceLocator.GetAll<IReactionService>()
        );
        
        SceneManager = new SceneManager(Simulation);
    }

    public System.Threading.Tasks.Task Start()
    {
        return Loop.Start();
    }

    public void Stop()
    {
        Loop.Stop();
    }

    public void Dispose()
    {
        Loop.Dispose();
        // State, Dispatcher, Scheduler, SceneManager typically don't need explicit disposal besides what Loop handles
    }
}
