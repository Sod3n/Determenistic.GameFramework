using System;
using System.Collections.Concurrent;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Network.Client;
using Deterministic.GameFramework.Network.Interfaces;

namespace Deterministic.GameFramework.DeltaSync;

public class DeltaSyncGameClient : GameClient
{
    private readonly DeltaSyncStrategyClient _delta;
    private readonly ConcurrentQueue<byte[]> _incomingTickDeltas = new();

    public DeltaSyncStrategyClient DeltaStrategy => _delta;

    /// <summary>
    /// Fired the first tick after a state catch-up (warm-up resync, baseline-gap resync, etc.)
    /// where the loop's tick number jumped non-contiguously. Subscribers can re-derive any
    /// view-side state that depends on the live ECS world.
    /// </summary>
    public event Action OnTickJump;

    public DeltaSyncGameClient(INetworkClient networkClient, string connectionString, Game game)
        : base(networkClient, connectionString, game)
    {
        _delta = new DeltaSyncStrategyClient();
        _delta.OnLog += m => Log($"[Delta] {m}");
        _delta.OnBaselineGap += HandleDeltaBaselineGap;
        _delta.OnWantTargetTick += target => _game.Loop.TargetTick = target;
        _game.Simulation.SetStrategy(_delta);

        _game.Loop.OnTick += WarmUpResyncTick;
        _game.Loop.OnTick += DetectTickJump;
    }

    protected override void SetupModeSpecificHandlers()
    {
        _networkClient.OnFullStateReceived += data => EnqueueWithDelay(data, 1);
        _networkClient.OnTickDeltaReceived += data => EnqueueWithDelay(data, 3);
    }

    protected override void EnqueueDirect(byte[] data, int type)
    {
        switch (type)
        {
            case 1: _incomingFullStates.Enqueue(data); break;
            case 3: _incomingTickDeltas.Enqueue(data); break;
        }
    }

    public override void DrainNetworkQueues()
    {
        base.DrainNetworkQueues();

        while (_incomingTickDeltas.TryDequeue(out var data))
            _delta.EnqueueServerDelta(data);
    }

    protected override void OnFullStateDeserialized(long tick, byte[] stateData)
    {
        _delta.OnFullStateApplied(tick, stateData);
    }

    protected override long GetBaseTickForExecute()
    {
        return System.Math.Max(Loop.CurrentTick, _delta.LastAppliedServerTick);
    }

    protected override long GetPredictionId()
    {
        return System.Threading.Interlocked.Increment(ref _nextPredictionId);
    }

    private long _nextPredictionId = 0;

    protected override void FlushPendingActions(System.Collections.Generic.List<PendingNetworkAction> pending)
    {
        long staleBefore = _delta.LastAppliedServerTick;
        pending.RemoveAll(a => a.ExecuteTick <= staleBefore);
    }

    private void HandleDeltaBaselineGap()
    {
        if (_isWaitingForFullState) return;
        Log("[Delta] Baseline gap - requesting full state resync.");
        _isWaitingForFullState = true;
        _fullStateRequestTick = Loop.CurrentTick;
        _ = RequestFullState();
    }

    // ── Warm-up resync ─────────────────────────────────────────────────────
    // The server's first N ticks are non-deterministic (scene load, navmesh bake, physics
    // settle). Instead of blocking startup, the client runs from tick 0 like normal; once
    // CurrentTick crosses WarmUpTicks, fire RequestFullState once so the authoritative
    // post-warm-up state replaces whatever we built up locally.
    private const long WarmUpTicks = 0;
    private bool _warmUpResyncRequested;
    private long _lastSeenLoopTick = -1;

    private void WarmUpResyncTick()
    {
        if (_warmUpResyncRequested) return;
        if (Loop.CurrentTick < WarmUpTicks) return;

        _warmUpResyncRequested = true;
        Log($"[Delta] Warm-up resync at tick {Loop.CurrentTick} — requesting FullState.");
        _ = RequestFullState();
        Loop.OnTick -= WarmUpResyncTick;
    }

    /// <summary>
    /// Detects Loop.ForceSetTick jumps (always caused by ApplyFullState) and forces an
    /// observer re-scan. Without this, Deserialize wipes the dirty-entity set so
    /// ArchetypeObserver's dirty-scan optimization misses FullState-imported entities,
    /// and reactive subscriptions never see onAdd for the freshly-imported entities.
    /// </summary>
    private void DetectTickJump()
    {
        long cur = Loop.CurrentTick;
        if (_lastSeenLoopTick >= 0 && cur != _lastSeenLoopTick + 1)
        {
            State.MarkAllDirty();
            Reactive.Reset();
            OnTickJump?.Invoke();
        }
        _lastSeenLoopTick = cur;
    }

    public override void Dispose()
    {
        Loop.OnTick -= WarmUpResyncTick;
        Loop.OnTick -= DetectTickJump;
        base.Dispose();
    }
}
