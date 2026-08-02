using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Serialization;

namespace Deterministic.GameFramework.GGPO;

public static class GGPOSetup
{
    public static StateHistory EnableGGPO(GameSimulation sim, int historyCapacity = 150, bool disableRollback = false)
    {
        var history = new StateHistory(historyCapacity);
        var strategy = new GGPOSyncStrategy();
        strategy.SetHistory(history);
        strategy.DisableRollback = disableRollback;
        sim.SetStrategy(strategy);

        if (history.GetLatestTick() < sim.CurrentTick)
            history.Store(sim.CurrentTick, sim.State);

        return history;
    }

    public static StateHistory? GetHistory(GameSimulation sim)
    {
        if (sim.Strategy is GGPOSyncStrategy ggpo)
            return ggpo.History;
        return null;
    }
}
