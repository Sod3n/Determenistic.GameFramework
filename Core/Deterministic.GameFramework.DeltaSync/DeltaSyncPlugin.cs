using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.DeltaSync;

public class DeltaSyncPlugin : ISyncModePlugin
{
    public void OnMatchCreated(Match match, GameSimulation sim, INetworkServer server)
    {
        var deltaStrategy = new DeltaSyncStrategyServer(server, match.Id);
        sim.SetStrategy(deltaStrategy);
        ILogger.Log($"[DeltaSyncPlugin] Match {match.Id} installed DeltaSyncStrategyServer.");
    }
}
