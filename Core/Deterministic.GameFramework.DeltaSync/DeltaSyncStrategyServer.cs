using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.DeltaSync;

public sealed class DeltaSyncStrategyServer : ISyncStrategy
{
    private readonly INetworkServer _networkServer;
    private readonly Guid _matchId;
    private readonly DeltaGenerator _generator = new();

    // Baseline = state as of the last broadcast. Delta is generated against this each tick.
    // Re-synced after each broadcast by deserializing the fresh snapshot into it.
    private EntityWorld _baseline = new();
    private long _baselineTick = -1;

    public DeltaGenerator Generator => _generator;

    /// <summary>Latest baseline tick this strategy has synced its mirror to.</summary>
    public long BaselineTick => _baselineTick;

    /// <summary>
    /// Rate (Hz) at which TickDelta packets are broadcast to clients. Sim itself still
    /// runs at the engine's full <see cref="IGameTime.FixedDeltaTime"/> cadence; this
    /// property throttles only the delta-generation + UDP send path.
    /// <para>
    /// <c>60</c> = per-tick broadcasts (matches sim rate). Lower values (e.g. <c>30</c>)
    /// cut broadcast CPU + bandwidth at the cost of client-visible update rate — clients
    /// already interpolate via ViewSmoother so the lower network rate is imperceptible.
    /// </para>
    /// </summary>
    public int BroadcastTickRate { get; set; } = 60;

    public DeltaSyncStrategyServer(INetworkServer networkServer, Guid matchId)
    {
        _networkServer = networkServer ?? throw new ArgumentNullException(nameof(networkServer));
        _matchId = matchId;
    }

    public void Attach(GameSimulation sim)
    {
        // Seed the baseline with the current live state so <c>_baselineTick</c> aligns with
        // the tick labeled in an immediate <c>CreateFullStatePacket</c>. Without this, a
        // client that requests FullState BEFORE the server's first <c>Tick</c> runs gets
        // <c>FullState(stateTick=0)</c>, then the server's first delta broadcasts
        // <c>(baseline=-1, serverTick=1)</c> — breaking the chain permanently.
        //
        // With this seed:
        //   FullState(stateTick=0) ← initial snapshot
        //   first delta: (baseline=0, serverTick=1) ← chains cleanly
        _baseline = new EntityWorld();
        var bytes = StateSerializer.Serialize(sim.State);
        StateSerializer.Deserialize(
            _baseline, bytes,
            syncComponentIds: false,
            autoReset: true,
            invalidateSystemData: false,
            fullInvalidate: false);
        _baselineTick = sim.CurrentTick;
    }

    public void Detach(GameSimulation sim)
    {
        _baseline = new EntityWorld();
        _baselineTick = -1;
    }

    /// <summary>
    /// Delta mode does no pre-simulation work. Inputs arrive via <c>OnBeforeTick</c>
    /// (drained by <see cref="GamePacketProcessor"/>), and the server never rolls back.
    /// </summary>
    public bool SuppressTickSnapshotBroadcast => true;
    public bool SuppressActionBroadcast => true;

    public bool PreSimulate(GameSimulation sim) => true;

    public void PostSimulate(GameSimulation sim)
    {
        // Always run per-tick housekeeping regardless of broadcast interval.
        sim.Scheduler.PruneHistory(sim.CurrentTick);
        sim.OnRecordTick?.Invoke(sim.CurrentTick, false, null);

        // Downsample broadcasts when BroadcastTickRate < sim tick rate. Baseline stays at
        // the last broadcast tick, so the NEXT delta spans the skipped ticks — client sees
        // serverTick jumps of `interval` and chains cleanly via baselineTick ==
        // _lastAppliedServerTick.
        var gameTime = sim.State.GetCustomData<IGameTime>();
        int simTickRate = gameTime != null && gameTime.FixedDeltaTime > (Float)0
            ? (int)(Float.One / gameTime.FixedDeltaTime)
            : 60;
        int rate = BroadcastTickRate > 0 ? BroadcastTickRate : simTickRate;
        int interval = rate >= simTickRate ? 1 : simTickRate / rate;
        if ((sim.CurrentTick % interval) != 0) return;

        // 1. Generate the delta from baseline → current into a pooled buffer when the
        //    underlying transport supports it. First tick has an empty baseline, so this
        //    produces EntityCreate ops for every live entity — effectively a full state.
        int headerSize = Marshal.SizeOf<TickDeltaHeader>();

        if (_networkServer is IPooledBroadcastNetworkServer pooledServer)
        {
            var (rented, length) = _generator.GenerateToPooledBuffer(sim.State, _baseline, sim.CurrentTick, _baselineTick);

            // 2a. Apply ops to baseline BEFORE handing ownership to the network server. The
            //     op stream lives at [headerSize, length) of the pooled buffer. We read it
            //     here while we still own the array; after BroadcastToGroupPooledAsync the
            //     buffer belongs to the pool again.
            if (length > headerSize)
            {
                var opStream = new ReadOnlySpan<byte>(rented, headerSize, length - headerSize);
                DeltaApplier.ApplyOps(_baseline, opStream);
            }

            // 3a. Broadcast. The network server copies bytes into its internal send queue
            //     synchronously and then returns the buffer to ArrayPool<byte>.Shared. The
            //     fire-and-forget pattern is safe because the returned Task is already
            //     complete by the time BroadcastToGroupPooledAsync returns.
            _ = pooledServer.BroadcastToGroupPooledAsync(_matchId.ToString(), rented, length, PacketType.TickDelta);
        }
        else
        {
            // Fallback for transports (SignalR, Composite) that don't implement the pooled
            // path. Preserves the pre-existing byte[] allocation pattern.
            byte[] packet = _generator.Generate(sim.State, _baseline, sim.CurrentTick, _baselineTick);
            _ = _networkServer.BroadcastToGroupAsync(_matchId.ToString(), packet, PacketType.TickDelta);

            if (packet.Length > headerSize)
            {
                var opStream = new ReadOnlySpan<byte>(packet, headerSize, packet.Length - headerSize);
                DeltaApplier.ApplyOps(_baseline, opStream);
            }
        }

        // Baseline now == state-at-this-tick. By construction, baseline + delta ==
        // current_state, so applying the op stream (above) leaves `_baseline` bit-
        // equivalent to deserializing sim.State — saves the full serialize/deserialize
        // round-trip per broadcast.
        _baselineTick = sim.CurrentTick;
    }
}
