using System;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public class Game : IDisposable
{
    public EntityWorld State { get; }
    public GameLoop Loop { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    public GameSimulation Simulation { get; }
    public GameLoopActionDispatcher GameLoopActionDispatcher { get; }

    public Game(EntityWorld? state = null, int tickRate = 60, int reserveEntityCapacity = 0)
    {
        State = state ?? new EntityWorld(reserveEntityCapacity);
        Scheduler = new ActionScheduler();
        Dispatcher = new Dispatcher();
        Simulation = new GameSimulation(State, Dispatcher, Scheduler);
        GameLoopActionDispatcher = new GameLoopActionDispatcher(Simulation);
        Dispatcher.ActionDispatcher = GameLoopActionDispatcher;
        Loop = new GameLoop(Simulation);
        Loop.SetTickRate(tickRate);

        Dispatcher.RegisterServices(
            ServiceLocator.GetAll<IActionService>()
        );
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
    }
}
