using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Serialization;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Client-side delta-sync strategy. Each tick:
/// <list type="number">
/// <item><c>PreSimulate</c>: drain deltas received from the network thread.
/// For each delta, verify <c>baselineTick</c> matches <see cref="_lastAppliedServerTick"/>,
/// then apply ops. For <see cref="DeltaOpKind.ComponentModify"/> ops, compute a
/// byte-arithmetic correction using the client's predicted delta captured at
/// <c>ServerTick</c>: <c>correction = (server_new_bytes - baseline_bytes) - predicted_bytes</c>.
/// Applying that correction on top of the client's current (possibly-diverged) state
/// converges it to the server's value while preserving post-server-tick prediction work.</item>
/// <item><c>PostSimulate</c>: diff the current world against <see cref="_lastAppliedBaseline"/>
/// and store the predicted-delta bytes in <see cref="_predicted"/> for future reconciliation.</item>
/// </list>
///
/// This strategy is a replacement for <see cref="GGPOSyncStrategy"/> — not a mixin.
/// Install it via <c>Simulation.SetStrategy(new DeltaSyncStrategyClient())</c> after
/// the initial full-state apply. The client <see cref="Deterministic.GameFramework.Network.Client.GameClient"/>
/// wires this up when constructed with <c>SyncMode.DeltaSync</c>.
/// </summary>
public class DeltaSyncStrategyClient : ISyncStrategy
{
    /// <summary>
    /// Server's last-confirmed world state. Used as the "pre-prediction" source when
    /// <see cref="DeltaSubtractor"/> needs to synthesize a revert op (client predicted a
    /// change server didn't make → restore baseline bytes). Updated in place each time a
    /// server delta is applied and replaced wholesale from a FullState via
    /// <see cref="OnFullStateApplied"/>.
    /// </summary>
    private readonly EntityWorld _lastAppliedBaseline = new();

    /// <summary>
    /// Snapshot of the client's state taken AFTER <see cref="PreSimulate"/> has applied any
    /// server deltas this tick, but BEFORE client sim runs. The diff against <c>sim.State</c>
    /// in <see cref="PostSimulate"/> is therefore purely the client's own sim work this tick —
    /// predicted actions and local systems — with no server-applied changes leaking in.
    /// <para>This is crucial: if it included server-applied changes, <see cref="DeltaSubtractor"/>
    /// would later see those as "client-only" ops (because the server delta at a DIFFERENT tick
    /// doesn't repeat them) and emit REVERT ops, destroying legitimate server entities on this
    /// client.</para>
    /// </summary>
    private readonly EntityWorld _prevClientTickWorld = new();

    /// <summary>
    /// Ring of predicted per-tick deltas keyed by client tick. Each slot stores the op-stream
    /// bytes of <c>state_at_tick_T − state_at_tick_T-1</c>. Evicted up to
    /// <c>lastAppliedServerTick</c> after each delta apply.
    /// </summary>
    private readonly PredictedDeltaRing _predicted = new(60);

    /// <summary>
    /// Deltas received from the LiteNetLib thread. Drained on the game loop thread at the start
    /// of each <see cref="PreSimulate"/>. Concurrent queue is safe for single-producer /
    /// single-consumer without locking.
    /// </summary>
    private readonly ConcurrentQueue<byte[]> _pendingServerDeltas = new();

    /// <summary>Tick of the most recent delta the client applied (or the FullState tick before any deltas).</summary>
    private long _lastAppliedServerTick = -1;

    /// <summary>Diffs <c>sim.State</c> against <see cref="_prevClientTickWorld"/> each PostSimulate.</summary>
    private readonly DeltaGenerator _generator = new();

    /// <summary>Reusable op-stream buffer for capturing per-tick predicted delta bytes.</summary>
    private byte[] _predictedScratch = new byte[8192];

    /// <summary>Reusable buffer for SnapshotCurrentIntoPrev — avoids per-tick byte[] allocation.</summary>
    private byte[] _snapshotBuffer = new byte[65536];

    /// <summary>Reusable writer for the <c>TargetDelta = serverDelta − clientDelta</c> output.</summary>
    private readonly DeltaOpWriter _targetWriter = new(8192);

    private GameSimulation? _sim;

    /// <summary>
    /// Per-action correlation state: when the client locally executes a predicted action,
    /// <see cref="EntityWorld.OnEntityCreated"/> fires and we record EACH new entity under
    /// the action's <c>PredictionId</c>. A single action can spawn multiple entities (e.g.
    /// <c>HouseDefinition.Create</c> builds the house + skin children), so we keep a list
    /// and pop one per matching server-delta arrival. When the server's authoritative
    /// delta later arrives for that same action (tagged with the same PredictionId via
    /// <see cref="PredictedByAction"/>), we look up the predicted ghost(s) and delete them.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<int>> _predictedEntitiesByPid = new();

    /// <summary>Tick at which each PredictionId was first recorded; used for orphan cleanup.</summary>
    private readonly System.Collections.Generic.Dictionary<long, long> _predictedEntityFirstSeenTick = new();

    /// <summary>
    /// How far ahead of the server the client should run, measured in ticks. Must exceed
    /// the one-way incoming latency (client learns about <c>serverTick=X</c> via a delta,
    /// but by the time it arrives the server is actually at <c>X + latencyTicks</c>). An
    /// action sent from the client arrives instantly on the server, so for the server to
    /// accept it we need <c>execTick > serverActualTick</c>, i.e.
    /// <c>clientTick + DefaultTickDelay > X + latencyTicks</c>.
    /// <para>
    /// At 60 Hz, 300 ms one-way latency = 18 ticks. A lead of 20 gives a 7-tick safety
    /// margin on top of <c>DefaultTickDelay=5</c>. For real deployments with lower
    /// latency (~50 ms = 3 ticks), this is generous but produces a correspondingly
    /// larger per-delta reconciliation window — <c>ViewSmoother</c>'s exponential lerp
    /// masks the snap.
    /// </para>
    /// </summary>
    public const int ClientLeadTicks = 20;

    /// <summary>
    /// Signaled when a gap is detected (<c>header.BaselineTick != _lastAppliedServerTick</c>).
    /// The client should respond by requesting a FullState resync.
    /// </summary>
    public event Action? OnBaselineGap;

    /// <summary>
    /// Fired when a client-predicted entity is replaced by its server-authoritative
    /// counterpart. Parameters: <c>(predictedId, serverId)</c>. Views that cache entity
    /// ids in dictionaries (e.g. <c>EntityViewModel.EntityViewModels</c>,
    /// <c>ViewSmoother._byEntity</c>) can rebind instead of tearing down.
    /// </summary>
    public event Action<int, int>? OnEntityRemapped;

    /// <summary>
    /// Fired after each delta apply, asking the host to push the simulation <c>Loop.TargetTick</c>
    /// to <c>serverTick + ClientLeadTicks</c>. This keeps the client running ahead of the server
    /// so future <c>Execute</c> calls produce <c>ExecuteTick</c>s that the server accepts. The
    /// strategy can't reach <c>GameLoop</c> directly — <see cref="Client.GameClient"/> wires this
    /// up at construction time.
    /// </summary>
    public event Action<long>? OnWantTargetTick;

    /// <summary>Optional logger sink — mirrors <see cref="Client.GameClient.OnLog"/>.</summary>
    public event Action<string>? OnLog;

    public long LastAppliedServerTick => _lastAppliedServerTick;
    public int PendingDeltaCount => _pendingServerDeltas.Count;

    public void Attach(GameSimulation sim)
    {
        _sim = sim;
        sim.State.OnEntityCreated += HandlePredictedEntityCreated;
    }

    public void Detach(GameSimulation sim)
    {
        sim.State.OnEntityCreated -= HandlePredictedEntityCreated;
        _sim = null;
    }

    /// <summary>
    /// Hook that runs from <see cref="EntityWorld.CreateEntity"/> right after the entity id
    /// has been allocated and the <see cref="PredictedByAction"/> tag applied (when
    /// <see cref="EntityWorld.CurrentActionPredictionId"/> is non-zero). We record the pair so
    /// <see cref="CorrelateServerEntity"/> can find the predicted ghost later.
    /// </summary>
    private void HandlePredictedEntityCreated(Entity entity)
    {
        if (_sim == null) return;
        long pid = _sim.State.CurrentActionPredictionId;
        if (pid == 0) return; // non-predicted (e.g. system-created) entity — skip
        if (!_predictedEntitiesByPid.TryGetValue(pid, out var list))
        {
            list = new System.Collections.Generic.List<int>();
            _predictedEntitiesByPid[pid] = list;
            _predictedEntityFirstSeenTick[pid] = _sim.CurrentTick;
        }
        list.Add(entity.Id);
        Log($"[Predict] client entity {entity.Id} created for PID={pid} at tick {_sim.CurrentTick} (group size now {list.Count})");
    }

    /// <summary>
    /// After a server delta applies an <see cref="DeltaOpKind.EntityCreate"/> or
    /// <see cref="DeltaOpKind.ComponentAdd"/> for a <see cref="PredictedByAction"/> component,
    /// the framework calls here. We look up the client's predicted counterpart by PID; if the
    /// ids differ, destroy the predicted ghost and fire <see cref="OnEntityRemapped"/> so
    /// views can rebind their entity references.
    /// </summary>
    private void CorrelateServerEntity(EntityWorld world, int serverEntityId)
    {
        if (serverEntityId < 0 || serverEntityId >= world.EntityMasks.Length) return;

        int predTagBit = ComponentId<PredictedByAction>.IntId;
        if (!world.EntityMasks[serverEntityId].IsSet(predTagBit)) return;

        long pid = world.GetComponent<PredictedByAction>(new Entity(serverEntityId)).PredictionId;
        Log($"[Predict] server delta carrying PredictedByAction(PID={pid}) for entity {serverEntityId}");
        if (pid == 0) return;

        if (!_predictedEntitiesByPid.TryGetValue(pid, out var list) || list.Count == 0)
        {
            // Server tagged an entity with a PID we never recorded locally — either it's
            // another client's prediction echoed through, or we've already correlated all
            // predicted entities for this PID. Harmless.
            Log($"[Predict] PID={pid} has no unmatched local prediction (maybe already correlated).");
            return;
        }

        int predictedId = list[0];
        list.RemoveAt(0);
        if (list.Count == 0)
        {
            _predictedEntitiesByPid.Remove(pid);
            _predictedEntityFirstSeenTick.Remove(pid);
        }

        if (predictedId == serverEntityId)
        {
            Log($"[Predict] PID={pid} id matched ({serverEntityId}); no remap needed.");
            return;
        }

        world.DeleteEntity(new Entity(predictedId));
        Log($"[Predict] Correlated PID={pid}: client {predictedId} → server {serverEntityId}; destroyed predicted ghost.");
        OnEntityRemapped?.Invoke(predictedId, serverEntityId);
    }

    /// <summary>
    /// Called from the network thread when a <see cref="PacketType.TickDelta"/> packet arrives.
    /// Thread-safe with the game loop thread via <see cref="ConcurrentQueue{T}"/>.
    /// </summary>
    public void EnqueueServerDelta(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return;
        _pendingServerDeltas.Enqueue(bytes);
        if ((++_enqueuedTotal % 60) == 1)
            Log($"[Delta] EnqueueServerDelta total={_enqueuedTotal} (lastAppliedServerTick={_lastAppliedServerTick})");
    }
    private long _enqueuedTotal;
    private long _preSimEntryCount;
    private long _appliedSummaryCount;

    /// <summary>
    /// Called by <see cref="Client.GameClient.ApplyFullState"/> after deserializing the
    /// authoritative state into <see cref="GameSimulation.State"/>. Resets the strategy's
    /// baseline and clears any stale predictions so the next incoming delta starts from a
    /// clean slate.
    ///
    /// Pending deltas: we DO NOT blindly drain the queue. During the FullState round-trip
    /// the server keeps broadcasting tick deltas, many of which will chain cleanly on top
    /// of this FullState (their <c>ServerTick &gt; tick</c>). Dropping those would guarantee
    /// an infinite gap → resync loop because the first post-drain delta always has a
    /// <c>BaselineTick</c> far ahead of <c>tick</c>. We keep the applicable deltas and only
    /// discard the stale ones.
    /// </summary>
    public void OnFullStateApplied(long tick, byte[] fullStateBytes)
    {
        _lastAppliedServerTick = tick;

        if (fullStateBytes != null && fullStateBytes.Length > 0)
        {
            // Deserialize into the baseline world. Use syncComponentIds=false so we inherit
            // the client's mappings (the caller has already adopted server mappings on the
            // live state via AdoptMappingsFrom), and skip derived-state invalidation since the
            // baseline is never simulated.
            StateSerializer.Deserialize(
                _lastAppliedBaseline,
                fullStateBytes,
                syncComponentIds: false,
                autoReset: true,
                invalidateSystemData: false,
                fullInvalidate: false);

            // Seed the prev-tick snapshot from the same FullState so the next PostSimulate's
            // per-tick diff doesn't report every entity as a fresh EntityCreate.
            StateSerializer.Deserialize(
                _prevClientTickWorld,
                fullStateBytes,
                syncComponentIds: false,
                autoReset: true,
                invalidateSystemData: false,
                fullInvalidate: false);
        }

        _predicted.Clear();

        // Filter the pending-delta queue: keep deltas with ServerTick > tick (applicable
        // on top of this FullState), drop everything else (stale — already encompassed).
        int kept = 0, dropped = 0;
        int headerSize = Marshal.SizeOf<TickDeltaHeader>();
        var drained = new System.Collections.Generic.List<byte[]>();
        while (_pendingServerDeltas.TryDequeue(out var data))
            drained.Add(data);

        foreach (var data in drained)
        {
            if (data.Length < headerSize) { dropped++; continue; }
            var hdr = MemoryMarshal.Read<TickDeltaHeader>(new ReadOnlySpan<byte>(data));
            if (hdr.ServerTick > tick)
            {
                _pendingServerDeltas.Enqueue(data);
                kept++;
            }
            else
            {
                dropped++;
            }
        }

        if (kept > 0 || dropped > 0)
            Log($"[Delta] FullState at tick {tick} applied. Requeued {kept} future delta(s), dropped {dropped} stale.");
    }

    public bool PreSimulate(GameSimulation sim)
    {
        int appliedCount = 0;
        int processed = 0;
        int queuedAtEntry = _pendingServerDeltas.Count;
        if (queuedAtEntry > 0 && (++_preSimEntryCount % 60) == 1)
            Log($"[Delta] PreSimulate entry: {queuedAtEntry} pending, lastAppliedServerTick={_lastAppliedServerTick}, simTick={sim.CurrentTick}");

        // Drain deltas in FIFO order.
        while (_pendingServerDeltas.TryDequeue(out var deltaBytes))
        {
            processed++;
            int headerSize = Marshal.SizeOf<TickDeltaHeader>();
            if (deltaBytes.Length < headerSize)
            {
                Log($"[Delta] dropped malformed packet (len={deltaBytes.Length} < header {headerSize})");
                continue;
            }

            var header = MemoryMarshal.Read<TickDeltaHeader>(new ReadOnlySpan<byte>(deltaBytes));

            // Already-applied / stale delta: skip silently. Happens when a delta arrives
            // whose server tick is <= the one we just installed via FullState — the FullState
            // already encompasses it. Dropping quietly (no gap event) keeps us from triggering
            // spurious resyncs while the client is catching up.
            if (header.ServerTick <= _lastAppliedServerTick)
                continue;

            // Gap detection: we require a contiguous chain of deltas starting from the tick
            // the client joined at (or the last FullState).
            if (header.BaselineTick != _lastAppliedServerTick)
            {
                Log($"[Delta] baseline gap — expected baseline={_lastAppliedServerTick}, " +
                    $"got {header.BaselineTick} (serverTick={header.ServerTick}). Requesting resync.");
                OnBaselineGap?.Invoke();
                return false; // Abort this tick entirely.
            }

            if (header.PayloadLength > 0 && headerSize + header.PayloadLength <= deltaBytes.Length)
            {
                var payload = new ReadOnlySpan<byte>(deltaBytes, headerSize, header.PayloadLength);
                ApplyDeltaOps(sim, payload, header.ServerTick);
            }

            _lastAppliedServerTick = header.ServerTick;
            _predicted.EvictUpTo(header.ServerTick);
            appliedCount++;
        }

        if (appliedCount > 0 && (++_appliedSummaryCount % 60) == 1)
            Log($"[Delta] PreSimulate applied {appliedCount} deltas this tick, processed={processed}, lastAppliedServerTick={_lastAppliedServerTick}");

        // After draining, ensure client's loop stays ahead of the server by ClientLeadTicks.
        // If wall-clock drift has left the client behind server, the game-loop's catch-up
        // logic (multi-tick-per-frame when CurrentTick < TargetTick) will close the gap
        // so future Execute calls produce ExecuteTicks that land in the server's future.
        if (_lastAppliedServerTick > 0)
        {
            long target = _lastAppliedServerTick + ClientLeadTicks;
            if (target > sim.CurrentTick) OnWantTargetTick?.Invoke(target);
        }

        if (appliedCount > 0)
        {
            // Coarse dirty marking: any modify/add/remove applied to a component isn't tracked
            // by EntityWorld's incremental dirty set (we wrote through stores directly). Mark
            // everything dirty so ReactiveSystem re-checks entities this tick. TODO: replace
            // with per-entity dirty marking driven by the applied op list.
            sim.State.MarkAllDirty();
        }

        // Snapshot state AFTER server deltas applied, BEFORE sim runs this tick. Any diff in
        // PostSimulate is then purely the client's own sim work (predictions + local systems)
        // — server-applied ops are excluded, so Subtract doesn't later mistake them for
        // client-only predictions and revert them.
        SnapshotCurrentIntoPrev(sim);

        return true;
    }

    private void SnapshotCurrentIntoPrev(GameSimulation sim)
    {
        var (stateBuf, stateLen) = StateSerializer.SerializeInto(sim.State);
        if (_snapshotBuffer.Length < stateLen)
            _snapshotBuffer = new byte[(int)(stateLen * 1.5)];
        Buffer.BlockCopy(stateBuf, 0, _snapshotBuffer, 0, stateLen);
        StateSerializer.Deserialize(
            _prevClientTickWorld, _snapshotBuffer, stateLen,
            syncComponentIds: false,
            autoReset: true,
            invalidateSystemData: false,
            fullInvalidate: false);
    }

    public void PostSimulate(GameSimulation sim)
    {
        // Diff the post-sim state against the pre-sim snapshot captured at the end of
        // PreSimulate → captures only client's own sim work for this tick (predictions +
        // local systems). Store in the ring keyed by client tick.
        var span = _generator.GenerateOpStream(sim.State, _prevClientTickWorld);
        EnsurePredictedScratch(span.Length);
        if (span.Length > 0)
            span.CopyTo(_predictedScratch);

        _predicted.Store(sim.CurrentTick, _predictedScratch, span.Length);

        // Prune scheduler history. Keep a small buffer of recent actions for bookkeeping (no
        // rollback, but ScheduleFromBytes needs to reject duplicate ticks that are still relevant).
        sim.Scheduler.PruneHistory(sim.CurrentTick - 30);

        // Orphaned-prediction cleanup — server rejected the action or the delta never arrived.
        PrunePredictedEntities(sim);
    }

    private const long PredictedEntityTimeoutTicks = 600; // 10s at 60 Hz

    private void PrunePredictedEntities(GameSimulation sim)
    {
        if (_predictedEntitiesByPid.Count == 0) return;

        System.Collections.Generic.List<long>? expired = null;
        foreach (var kvp in _predictedEntityFirstSeenTick)
        {
            if (sim.CurrentTick - kvp.Value <= PredictedEntityTimeoutTicks) continue;
            expired ??= new System.Collections.Generic.List<long>();
            expired.Add(kvp.Key);
        }
        if (expired == null) return;

        foreach (var pid in expired)
        {
            var list = _predictedEntitiesByPid[pid];
            long firstSeen = _predictedEntityFirstSeenTick[pid];
            _predictedEntitiesByPid.Remove(pid);
            _predictedEntityFirstSeenTick.Remove(pid);
            Log($"[Predict] Orphaned PID={pid} after {sim.CurrentTick - firstSeen} ticks; destroying {list.Count} predicted entit(y|ies).");
            foreach (var entityId in list)
            {
                sim.State.DeleteEntity(new Entity(entityId));
            }
        }
    }

    /// <summary>
    /// Reconciles the server's delta for <paramref name="serverTick"/> against the client's
    /// per-tick predicted delta recorded at the same tick. Uses the algebraic
    /// <c>TargetDelta = ServerDelta − ClientDelta</c> identity: matching ops cancel (no snap),
    /// mismatched ops surface the server's value (snap), and client-only predictions that the
    /// server didn't echo are reverted via baseline bytes.
    ///
    /// <para>PredictedByAction correlation runs first so the server-authoritative entity id can
    /// replace the client's predicted id (destroy predicted, fire OnEntityRemapped). After that,
    /// the algebra handles everything else — including auto-revert of predicted entities whose
    /// server counterpart never arrived.</para>
    ///
    /// Also updates <see cref="_lastAppliedBaseline"/> to reflect the server's state as of
    /// <paramref name="serverTick"/>.
    /// </summary>
    private void ApplyDeltaOps(GameSimulation sim, ReadOnlySpan<byte> opStream, long serverTick)
    {
        // Step 1: PredictedByAction correlation pre-pass. Any server EntityCreate / ComponentAdd
        // carrying a pid we recorded locally → destroy the predicted ghost and fire the remap
        // event. This keeps the existing behaviour; views that still use predicted ids get their
        // tear-down + respawn signals through the reactive observers.
        {
            var scanReader = new DeltaOpReader(opStream);
            while (scanReader.TryReadOp(out var op))
            {
                if (op.Kind == DeltaOpKind.EntityCreate)
                {
                    CorrelateServerEntity(sim.State, op.EntityId);
                }
                else if (op.Kind == DeltaOpKind.ComponentAdd
                    && op.DenseId == ComponentId<PredictedByAction>.IntId)
                {
                    CorrelateServerEntity(sim.State, op.EntityId);
                }
            }
        }

        // Step 2: compute TargetDelta = serverDelta − clientPredictedDelta[serverTick].
        ReadOnlySpan<byte> clientDelta = ReadOnlySpan<byte>.Empty;
        if (_predicted.TryGet(serverTick, out var clientBytes, out int clientLen))
            clientDelta = new ReadOnlySpan<byte>(clientBytes, 0, clientLen);

        _targetWriter.Reset();
        DeltaSubtractor.Subtract(opStream, clientDelta, _lastAppliedBaseline, _targetWriter);

        // Debug: log EntityCreate ops in server delta and whether they made it into target delta.
        LogEntityCreates(opStream, _targetWriter.WrittenSpan, serverTick);

        // Step 3: apply TargetDelta to live state. If prediction was perfect, this is a no-op.
        // Otherwise, it's the minimal set of snap/revert ops needed to reconcile to server.
        var targetSpan = _targetWriter.WrittenSpan;
        if (!targetSpan.IsEmpty)
            DeltaApplier.ApplyOps(sim.State, targetSpan);

        // Step 4: baseline always advances to reflect the server's authoritative state at this tick.
        DeltaApplier.ApplyOps(_lastAppliedBaseline, opStream);
    }

    private void EnsurePredictedScratch(int needed)
    {
        if (_predictedScratch.Length < needed)
            _predictedScratch = new byte[(int)(needed * 1.25) + 16];
    }

    /// <summary>
    /// Temporary diagnostic: inspects a server delta for EntityCreate ops and checks whether each
    /// one survived subtraction (i.e., produced an EntityCreate in the TargetDelta). If a server
    /// EntityCreate is MISSING from TargetDelta, that means Subtract judged the client already had
    /// an identical EntityCreate — which for a non-predicting client means a bug or id collision.
    /// </summary>
    private void LogEntityCreates(ReadOnlySpan<byte> serverOps, ReadOnlySpan<byte> targetOps, long serverTick)
    {
        // Collect server entity-creates.
        var serverCreates = new System.Collections.Generic.List<int>();
        var serverReader = new DeltaOpReader(serverOps);
        while (serverReader.TryReadOp(out var op))
        {
            if (op.Kind == DeltaOpKind.EntityCreate)
                serverCreates.Add(op.EntityId);
        }
        if (serverCreates.Count == 0) return;

        // Collect target entity-creates.
        var targetCreates = new System.Collections.Generic.HashSet<int>();
        var targetReader = new DeltaOpReader(targetOps);
        while (targetReader.TryReadOp(out var op))
        {
            if (op.Kind == DeltaOpKind.EntityCreate)
                targetCreates.Add(op.EntityId);
        }

        foreach (var id in serverCreates)
        {
            if (targetCreates.Contains(id))
                Log($"[Subtract] serverTick={serverTick} server EntityCreate({id}) → emitted into TargetDelta (will be applied)");
            else
                Log($"[Subtract] serverTick={serverTick} server EntityCreate({id}) → SKIPPED by subtract (client had matching op — potential id-collision bug if this client is non-predicting)");
        }
    }

    private void Log(string msg) => OnLog?.Invoke(msg);
}
