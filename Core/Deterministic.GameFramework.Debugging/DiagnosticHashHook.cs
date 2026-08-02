using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Debugging;

public class DiagnosticHashHook
{
    public long TickMin { get; set; } = 0;
    public long TickMax { get; set; } = long.MaxValue;

    public void Attach(GameSimulation sim)
    {
        sim.AfterActionsDispatch = OnAfterActionsDispatch;
        sim.SystemRunner.AfterSystemUpdate = OnAfterSystemUpdate;
    }

    public void Detach(GameSimulation sim)
    {
        sim.AfterActionsDispatch = null;
        sim.SystemRunner.AfterSystemUpdate = null;
    }

    private void OnAfterActionsDispatch(EntityWorld state, long tick)
    {
        if (tick >= TickMin && tick <= TickMax && tick % 60 == 0 && tick > 0)
        {
            var hash = StateHasher.Hash(state);
            ILogger.Log($"[SimHash] tick={tick} after Actions+Dispatch: {hash}");
        }
    }

    private void OnAfterSystemUpdate(EntityWorld state, ISystem system)
    {
        var gameTime = state.GetCustomData<IGameTime>();
        long tick = gameTime?.CurrentTick ?? -1;
        if (tick >= TickMin && tick <= TickMax && tick % 60 == 0 && tick > 0)
        {
            var hash = StateHasher.Hash(state);
            ILogger.Log($"[SystemHash] tick={tick} after {system.GetType().Name}: {hash}");
        }
    }
}
