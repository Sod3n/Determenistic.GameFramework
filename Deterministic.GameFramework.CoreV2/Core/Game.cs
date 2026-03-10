using System;
using Deterministic.GameFramework.CoreV2.Scene;

namespace Deterministic.GameFramework.CoreV2;

/// <summary>
/// Central entry point for the deterministic game simulation.
/// Encapsulates the core systems: State, Loop, Dispatcher, Scheduler, and SceneManager.
/// </summary>
public class Game : IDisposable
{
    public GlobalState State { get; }
    public GameLoop Loop { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    public SceneManager SceneManager { get; }

    public Game(GlobalState? state = null, int tickRate = 60)
    {
        // 1. Setup State
        State = state ?? new GlobalState();
        
        // 2. Setup Dispatcher
        // Use ComponentId for ID mapping
        Dispatcher = new Dispatcher(type => ComponentId.FromType(type).ToStable());
        
        // 3. Setup Scheduler & Loop
        Scheduler = new ActionScheduler();
        Loop = new GameLoop(State, Dispatcher, Scheduler);
        Loop.SetTickRate(tickRate);
        
        // 4. Register All Services from ServiceLocator
        // This ensures all actions/reactions are known and have RuntimeIDs assigned
        Dispatcher.RegisterServices(
            ServiceLocator.GetAll<IActionService>(), 
            ServiceLocator.GetAll<IReactionService>()
        );
        
        // 5. Setup Scene Manager
        SceneManager = new SceneManager(Loop);
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
