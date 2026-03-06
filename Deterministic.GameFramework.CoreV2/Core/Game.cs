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
        // Use ServiceLocator for ID mapping
        Dispatcher = new Dispatcher(type => ServiceLocator.TypeToId[type]);
        
        // 3. Setup Scheduler & Loop
        Scheduler = new ActionScheduler();
        Loop = new GameLoop(State, Dispatcher, Scheduler);
        Loop.SetTickRate(tickRate); 
        
        // 4. Setup Scene Manager
        SceneManager = new SceneManager(Loop);
        
        // 5. Initialize Services (Actions/Reactions are global)
        ServiceLocator.Initialize(Dispatcher);
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
