using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.GGPO;

public class GGPOSyncStrategy : ISyncStrategy
{
    private StateHistory? _history;

    public StateHistory? History => _history;
    public bool DisableRollback { get; set; }

    public void Attach(GameSimulation sim) { }
    public void Detach(GameSimulation sim) { }

    internal void SetHistory(StateHistory history)
    {
        _history = history;
    }

    public bool PreSimulate(GameSimulation sim)
    {
        if (DisableRollback) return true;
        if (_history == null) return true;

        long dirtyTick = sim.Scheduler.EarliestDirtyTick;
        if (dirtyTick >= sim.CurrentTick) return true;

        long originalTick = sim.CurrentTick;
        long restoreTick = dirtyTick;

        if (_history.Retrieve(restoreTick, sim.State))
        {
            ILogger.Log($"[Rollback] Rolling back from {sim.CurrentTick} to {restoreTick} (Input at {dirtyTick})");

            _history.DiscardFuture(restoreTick);
            sim.SetCurrentTick(restoreTick);

            sim.IsResimulating = true;
            while (sim.CurrentTick < originalTick)
            {
                sim.SimulateStep();
                sim.AdvanceTick();
                _history.Store(sim.CurrentTick, sim.State);

                if (_history.TryGetSnapshotData(sim.CurrentTick, out byte[]? snapData))
                    sim.OnRecordTick?.Invoke(sim.CurrentTick, true, snapData);
                else
                    sim.OnRecordTick?.Invoke(sim.CurrentTick, true, null);
            }
            sim.IsResimulating = false;

            OnRollbackComplete?.Invoke();
            return true;
        }

        ILogger.LogError($"[Rollback] Failed to restore state for tick {restoreTick}. " +
            $"History range: {_history.GetOldestTick()}-{_history.GetLatestTick()}. Requesting sync.");
        OnRollbackFailed?.Invoke();
        return false;
    }

    public void PostSimulate(GameSimulation sim)
    {
        if (_history == null) return;

        _history.Store(sim.CurrentTick, sim.State);

        if (_history.TryGetSnapshotData(sim.CurrentTick, out byte[]? snapData))
            sim.OnRecordTick?.Invoke(sim.CurrentTick, false, snapData);
        else
            sim.OnRecordTick?.Invoke(sim.CurrentTick, false, null);

        long oldestTick = _history.GetOldestTick();
        if (oldestTick > 0)
            sim.Scheduler.PruneHistory(oldestTick);
    }

    internal event System.Action? OnRollbackFailed;
    internal event System.Action? OnRollbackComplete;
}
