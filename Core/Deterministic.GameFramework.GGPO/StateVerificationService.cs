using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.GGPO;

public class StateVerificationService : IDisposable
{
    private readonly Match _match;
    private readonly INetworkServer _networkServer;
    private readonly StateHistory _history;
    private readonly GGPOSyncStrategy _strategy;
    private readonly int _intervalTicks;
    private bool _disposed;

    public int ConfirmationWindowTicks { get; set; } = GameSimulation.ConfirmationWindowTicks;

    public StateVerificationService(Match match, INetworkServer networkServer, StateHistory history, GGPOSyncStrategy strategy, int intervalTicks = 60)
    {
        ILogger.Log($"[StateVerificationService] Initialized for Match {match.Id}. Interval: {intervalTicks} ticks.");
        _match = match;
        _networkServer = networkServer;
        _history = history;
        _strategy = strategy;
        _intervalTicks = intervalTicks;
        _match.Loop.OnTick += OnTick;
    }

    private void OnTick()
    {
        if (_disposed) return;

        long currentTick = _match.Loop.CurrentTick;
        bool noRollback = _strategy.DisableRollback;

        long minAllowed = noRollback
            ? currentTick
            : currentTick - ConfirmationWindowTicks;
        if (minAllowed > 0)
            _match.Loop.Simulation.Scheduler.PruneHistory(minAllowed);

        if (currentTick % _intervalTicks == 0)
        {
            long verifyTick = noRollback
                ? currentTick
                : currentTick - ConfirmationWindowTicks;
            if (verifyTick <= 0) return;

            if (!_history.TryGetSnapshotData(verifyTick, out byte[]? snapshotData))
            {
                ILogger.LogWarning($"[StateVerification] Tick {verifyTick} not in history, skipping.");
                return;
            }

            var hash = StateHasher.Hash(snapshotData!);

            var packet = new StateHashPacket
            {
                Tick = verifyTick,
                Hash = (System.Guid)hash
            };

            BroadcastHash(packet);
        }
    }

    private void BroadcastHash(StateHashPacket packet)
    {
        ILogger.Log($"[StateVerification] Broadcasting Hash for Tick {packet.Tick}: {packet.Hash}");
        int size = Marshal.SizeOf<StateHashPacket>();
        byte[] data = new byte[size];

        var span = new Span<byte>(data);
        MemoryMarshal.Write(span, ref packet);

        _ = _networkServer.BroadcastToGroupAsync(_match.Id.ToString(), data, PacketType.StateHash);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _match.Loop.OnTick -= OnTick;
    }
}
