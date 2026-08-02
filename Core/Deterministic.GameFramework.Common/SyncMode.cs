namespace Deterministic.GameFramework.Common;

/// <summary>
/// Networking synchronization strategy. Chosen at match creation time; must match on
/// client and server. Installed into <see cref="GameSimulation.Strategy"/> via
/// <see cref="GameSimulation.SetStrategy"/> — see <see cref="ISyncStrategy"/>.
/// </summary>
public enum SyncMode : byte
{
    /// <summary>
    /// Deterministic lockstep with rollback + resimulation. Clients run ahead of server by
    /// <see cref="GameSimulation.ConfirmationWindowTicks"/>; inputs are redundantly resent
    /// until acknowledged; state hashes are periodically verified. See <c>GGPOSyncStrategy</c>.
    /// </summary>
    GGPO = 0,

    /// <summary>
    /// Server-authoritative delta sync. Server broadcasts per-tick component deltas; clients
    /// predict locally and reconcile on incoming deltas via byte-arithmetic correction. No
    /// rollback, no confirmation window. See <c>DeltaSyncStrategyServer</c> /
    /// <c>DeltaSyncStrategyClient</c>.
    /// </summary>
    DeltaSync = 1
}
