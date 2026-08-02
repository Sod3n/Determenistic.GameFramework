using Deterministic.GameFramework.Common;

namespace Deterministic.GameFramework.Network.Server;

public class MatchmakingOptions
{
    public int MinPlayersPerMatch { get; set; } = 2;
    public int MaxPlayersPerMatch { get; set; } = 2;

    /// <summary>
    /// Networking sync strategy installed on matches the matchmaker creates.
    /// GGPO (default): deterministic lockstep with rollback. DeltaSync: server-authoritative
    /// per-tick component deltas with client prediction. Must match the client's
    /// <c>GameClient</c> construction mode.
    /// </summary>
    public SyncMode SyncMode { get; set; } = SyncMode.GGPO;
}
