using System;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Common;

public class GameSimulation
{
    public const int ConfirmationWindowTicks = 120;

    public EntityWorld State { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    public SystemRunner SystemRunner { get; }

    public long CurrentTick { get; private set; }
    public bool IsResimulating { get; internal set; }

    public Action<long, bool, byte[]?>? OnRecordTick { get; set; }
    public Action<EntityWorld, long>? AfterActionsDispatch { get; set; }

    public event Action? OnBeforeTick;
    public event Action? OnTick;

    private ISyncStrategy _strategy;

    public ISyncStrategy Strategy => _strategy;

    public GameSimulation(EntityWorld state, Dispatcher dispatcher, ActionScheduler scheduler)
    {
        State = state;
        Dispatcher = dispatcher;
        Scheduler = scheduler;
        SystemRunner = new SystemRunner();
        _strategy = new NullSyncStrategy();
        _strategy.Attach(this);
    }

    public void SetStrategy(ISyncStrategy strategy)
    {
        if (strategy == null) throw new ArgumentNullException(nameof(strategy));
        _strategy.Detach(this);
        _strategy = strategy;
        _strategy.Attach(this);
    }

    public void ForceSetTick(long tick)
    {
        CurrentTick = tick;
    }

    internal void SetCurrentTick(long tick) => CurrentTick = tick;

    public void AdvanceTick() => CurrentTick++;

    public void Schedule<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        var denseId = ComponentId<TAction>.DenseId;
        Scheduler.Schedule(action, denseId, target, CurrentTick);
    }

    public void ScheduleOnTick<TAction>(long tick, TAction action, Entity target) where TAction : struct, IAction
    {
        var denseId = ComponentId<TAction>.DenseId;
        Scheduler.Schedule(action, denseId, target, tick);
    }

    public void Tick()
    {
        try
        {
            OnBeforeTick?.Invoke();
        }
        catch (Exception ex)
        {
            ILogger.LogError($"[GameSimulation] Error in OnBeforeTick listener: {ex.Message}");
        }

        if (!_strategy.PreSimulate(this))
        {
            return;
        }

        SimulateStep();
        AdvanceTick();

        _strategy.PostSimulate(this);

        try
        {
            OnTick?.Invoke();
        }
        catch (Exception ex)
        {
            ILogger.LogError($"[GameSimulation] Error in OnTick listener: {ex.Message}");
        }

        State.ClearDirty();
    }

    public void SimulateStep()
    {
        Scheduler.ExecuteActions(CurrentTick, State, Dispatcher);

        Dispatcher.Update(State);

        AfterActionsDispatch?.Invoke(State, CurrentTick);

        SystemRunner.Update(State);
    }
}
