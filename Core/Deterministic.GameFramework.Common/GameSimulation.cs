using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public class GameSimulation
{
    public EntityWorld State { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    public SystemRunner SystemRunner { get; }
    public StateHistory History { get; }

    public long CurrentTick { get; private set; }
    public bool IsResimulating { get; private set; }
    
    public event Action? OnTick;
    public event Action? OnRollbackFailed;

    public GameSimulation(EntityWorld state, Dispatcher dispatcher, ActionScheduler scheduler)
    {
        State = state;
        Dispatcher = dispatcher;
        Scheduler = scheduler;
        SystemRunner = new SystemRunner();
        History = new StateHistory(300); // 5 seconds @ 60hz
    }

    public void ForceSetTick(long tick)
    {
        CurrentTick = tick;
    }

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
        long dirtyTick = Scheduler.EarliestDirtyTick;
        if (dirtyTick < CurrentTick)
        {
            // Rollback required!
            long originalTick = CurrentTick;
            long restoreTick = dirtyTick;
            
            // Try to find snapshot
            // Note: StateHistory now works with EntityWorld, but GlobalState delegates to it.
            // We need to pass EntityWorld to Retrieve.
            if (History.Retrieve(restoreTick, State))
            {
                Console.WriteLine($"[Rollback] Rolling back from {CurrentTick} to {restoreTick} (Input at {dirtyTick})");
                
                // Truncate the "False Future"
                History.DiscardFuture(restoreTick);
                
                CurrentTick = restoreTick;
                
                // RESIMULATION LOOP (Catch up to where we were)
                IsResimulating = true;
                while (CurrentTick < originalTick)
                {
                    SimulateStep();
                    CurrentTick++;
                    History.Store(CurrentTick, State);
                }
                IsResimulating = false;
            }
            else
            {
                Console.WriteLine($"[Rollback] Failed to restore state for tick {restoreTick}. History range: {History.GetOldestTick()}-{History.GetLatestTick()}. Requesting sync.");
                OnRollbackFailed?.Invoke();
                return; // Abort tick, wait for sync
            }
        }

        // 1. Simulate (Normal Step)
        SimulateStep();
        
        CurrentTick++;
        
        // 2. Save State to History
        History.Store(CurrentTick, State);

        // 3. Fire OnTick (After state is updated and stored, and Tick incremented)
        try
        {
            OnTick?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSimulation] Error in OnTick listener: {ex.Message}");
        }
        
        // 4. Prune Old Data
        long oldestTick = History.GetOldestTick();
        if (oldestTick > 0)
        {
            Scheduler.PruneHistory(oldestTick);
        }
        
        // 5. Clear Dirty State
        State.ClearDirty();
    }

    private void SimulateStep()
    {
        // 1. Apply Actions (Add Components)
        Scheduler.ExecuteActions(CurrentTick, State, Dispatcher);

        // 2. Process Actions
        Dispatcher.Update(State);

        // 3. Run Systems (Process Logic & Action Components)
        SystemRunner.Update(State);
    }
}
