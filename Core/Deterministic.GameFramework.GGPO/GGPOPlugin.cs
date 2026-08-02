using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.GGPO;

public class GGPOPlugin : ISyncModePlugin
{
    public void OnMatchCreated(Match match, GameSimulation sim, INetworkServer server)
    {
        var history = new StateHistory(150);
        var strategy = new GGPOSyncStrategy();
        strategy.SetHistory(history);
        strategy.DisableRollback = true;
        sim.SetStrategy(strategy);

        history.Store(sim.CurrentTick, sim.State);

        match.FullStateProvider = new GGPOFullStateProvider(history);

        var verificationService = new StateVerificationService(match, server, history, strategy);
        match.AddService(verificationService);

        ILogger.Log($"[GGPOPlugin] Match {match.Id} installed GGPOSyncStrategy + StateVerificationService.");
    }
}
