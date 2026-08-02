using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.Debugging;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Client;
using Deterministic.GameFramework.Network.Interfaces;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Serialization;

namespace Deterministic.GameFramework.GGPO;

public class GGPOGameClient : GameClient
{
    private readonly StateHistory _history;
    private readonly GGPOSyncStrategy _strategy;

    private long _hashGraceUntilTick = -1;
    private const int HashGraceTicks = 2;

    private int _consecutiveMismatches = 0;
    private const int ConsecutiveMismatchThreshold = 2;

    private readonly ConcurrentQueue<byte[]> _incomingTickSnapshots = new();
    private readonly ConcurrentQueue<byte[]> _incomingStateHashes = new();
    private readonly System.Collections.Generic.Dictionary<long, StateHashPacket> _pendingHashes = new();

    public StateHistory History => _history;

    public GGPOGameClient(INetworkClient networkClient, string connectionString, Game game)
        : base(networkClient, connectionString, game)
    {
        _history = new StateHistory(150);
        _strategy = new GGPOSyncStrategy();
        _strategy.SetHistory(_history);
        _strategy.OnRollbackFailed += OnRollbackFailed;
        _strategy.OnRollbackComplete += () => Reactive.Reset();
        _game.Simulation.SetStrategy(_strategy);

        _game.Loop.OnTick += ProcessPendingHashes;
    }

    protected override void SetupModeSpecificHandlers()
    {
        _networkClient.OnTickSnapshotReceived += data => EnqueueWithDelay(data, 0);
        _networkClient.OnFullStateReceived += data => EnqueueWithDelay(data, 1);
        _networkClient.OnStateHashReceived += data => EnqueueWithDelay(data, 2);
    }

    protected override void EnqueueDirect(byte[] data, int type)
    {
        switch (type)
        {
            case 0: _incomingTickSnapshots.Enqueue(data); break;
            case 1: _incomingFullStates.Enqueue(data); break;
            case 2: _incomingStateHashes.Enqueue(data); break;
        }
    }

    public override void DrainNetworkQueues()
    {
        base.DrainNetworkQueues();

        while (_incomingTickSnapshots.TryDequeue(out var data))
        {
            ApplyTickSnapshot(data);
        }

        while (_incomingStateHashes.TryDequeue(out var data))
        {
            ApplyStateHash(data);
        }
    }

    private void ApplyTickSnapshot(byte[] packetData)
    {
        var packetSpan = new ReadOnlySpan<byte>(packetData);
        int headerSize = Marshal.SizeOf<TickSnapshotHeader>();

        if (packetSpan.Length < headerSize) return;

        var header = MemoryMarshal.Read<TickSnapshotHeader>(packetSpan);
        var payloadSpan = packetSpan.Slice(headerSize, header.PayloadLength);

        Log($"Received TickSnapshot: ServerTick={header.ServerTick}, PayloadLength={header.PayloadLength}");

        if (header.MinAllowedTick > 0)
        {
            lock (_unconfirmedPredictions)
            {
            }
        }

        int offset = 0;
        int actionHeaderSize = Marshal.SizeOf<NetworkActionHeader>();

        while (offset + actionHeaderSize <= payloadSpan.Length)
        {
            var actionHeader = MemoryMarshal.Read<NetworkActionHeader>(payloadSpan.Slice(offset));
            offset += actionHeaderSize;

            if (offset + actionHeader.DataLength > payloadSpan.Length) break;

            var dataSpan = payloadSpan.Slice(offset, actionHeader.DataLength);
            offset += actionHeader.DataLength;

            string actionName = ComponentId.TryGetType(actionHeader.ComponentId, out var actionType) && actionType != null
                ? actionType.Name : $"CID({actionHeader.ComponentId})";

            bool isBumped = actionHeader.OriginalExecuteTick != 0 &&
                            actionHeader.OriginalExecuteTick != actionHeader.ExecuteTick;

            Log($"[TickSnapshot] Action: {actionName} target={actionHeader.TargetEntityId} " +
                $"execTick={actionHeader.ExecuteTick}" +
                (isBumped ? $" BUMPED from {actionHeader.OriginalExecuteTick}" : "") +
                $" (clientTick={Loop.CurrentTick})");

            if (actionHeader.OriginalExecuteTick != 0 &&
                actionHeader.OriginalExecuteTick != actionHeader.ExecuteTick)
            {
                for (int i = _unconfirmedPredictions.Count - 1; i >= 0; i--)
                {
                    var pred = _unconfirmedPredictions[i];
                    if (pred.ComponentId == actionHeader.ComponentId &&
                        pred.TargetEntityId == actionHeader.TargetEntityId &&
                        pred.ExecuteTick == actionHeader.OriginalExecuteTick &&
                        pred.Data.Length == actionHeader.DataLength &&
                        dataSpan.SequenceEqual(new ReadOnlySpan<byte>(pred.Data)))
                    {
                        Scheduler.RemoveAction(pred.ComponentId, pred.TargetEntityId, pred.ExecuteTick);
                        _unconfirmedPredictions.RemoveAt(i);
                        Log($"[Prediction] Reconciled bump: {actionHeader.ComponentId} on Entity {actionHeader.TargetEntityId} " +
                            $"tick {actionHeader.OriginalExecuteTick} -> {actionHeader.ExecuteTick}");
                        break;
                    }
                }
            }

            Scheduler.ScheduleFromBytes(actionHeader.ComponentId, dataSpan, actionHeader.TargetEntityId, actionHeader.ExecuteTick);
        }

        long targetTick = header.ServerTick + GameSimulation.ConfirmationWindowTicks;
        long delta = targetTick - Loop.CurrentTick;
        Loop.TargetTick = targetTick;
        if (Math.Abs(delta) > 60)
        {
            Log($"Tick Drift: {delta}. Server: {header.ServerTick}, Target: {targetTick}, Client: {Loop.CurrentTick}");
        }
    }

    protected override void ApplyFullState(byte[] packetData)
    {
        try
        {
            var packetSpan = new ReadOnlySpan<byte>(packetData);
            int headerSize = Marshal.SizeOf<FullStateHeader>();

            if (packetSpan.Length < headerSize)
            {
                Log("Invalid packet: too small for header");
                return;
            }

            var header = MemoryMarshal.Read<FullStateHeader>(packetSpan);
            var stateData = packetSpan.Slice(headerSize, header.StateDataLength).ToArray();

            Log($"Received Full State for Tick {header.Tick}. Size: {stateData.Length} bytes");

            long clientTickBeforeSync = Loop.CurrentTick;

            if (_isWaitingForFullState)
            {
                try
                {
                    byte[] localData;
                    if (_history.TryGetSnapshotData(header.Tick, out byte[]? snapshotData))
                    {
                        localData = snapshotData!;
                        Log($"[StateDiff] Comparing history snapshot at tick {header.Tick} (client was at tick {clientTickBeforeSync})");
                    }
                    else
                    {
                        localData = StateSerializer.Serialize(State);
                        Log($"[StateDiff] WARNING: Tick {header.Tick} not in history, falling back to live state at tick {clientTickBeforeSync}");
                    }
                    StateDumper.LogStateDiff("Client", header.Tick, localData, stateData);
                }
                catch (Exception ex)
                {
                    Log($"[StateDiff] Failed to diff states: {ex.Message}");
                }
            }

            StateSerializer.AdoptMappingsFrom(stateData);

            Log("Deserializing state...");
            StateSerializer.Deserialize(State, stateData, syncComponentIds: false, fullInvalidate: true);
            Log("State deserialized!");

            Log($"Setting tick to {header.Tick}...");
            Loop.ForceSetTick(header.Tick);

            _history.Store(header.Tick, State);

            Log($"Tick set to {header.Tick}!");

            Scheduler.PruneHistory(header.Tick);

            _isWaitingForFullState = false;
            _consecutiveMismatches = 0;

            int removedCount = _unconfirmedPredictions.RemoveAll(p => p.ExecuteTick < header.Tick);
            if (removedCount > 0)
                Log($"[Resync] Pruned {removedCount} old predictions (execTick < {header.Tick})");

            int reAddedCount = 0;
            foreach (var predicted in _unconfirmedPredictions)
            {
                var result = Scheduler.ScheduleFromBytes(
                    predicted.ComponentId, predicted.Data,
                    predicted.TargetEntityId, predicted.ExecuteTick);
                if (result == ActionScheduler.ScheduleResult.Success)
                    reAddedCount++;
            }
            if (reAddedCount > 0)
                Log($"[Resync] Re-added {reAddedCount} predicted actions (execTick >= {header.Tick})");

            long serverTick = header.ServerTick > 0 ? header.ServerTick : header.Tick;

            Reactive.IsPaused = true;

            _hashGraceUntilTick = serverTick + HashGraceTicks;

            if (serverTick > Loop.CurrentTick)
                Loop.TargetTick = serverTick;

            Log("Completing sync task...");
            _syncTcs.TrySetResult(true);
        }
        catch (Exception ex)
        {
            Log($"Error processing Full State: {ex}");
            _syncTcs.TrySetException(ex);
            _isWaitingForFullState = false;
        }
    }

    private void ApplyStateHash(byte[] data)
    {
        try
        {
            var span = new ReadOnlySpan<byte>(data);
            if (span.Length < Marshal.SizeOf<StateHashPacket>()) return;

            var packet = MemoryMarshal.Read<StateHashPacket>(span);

            if (packet.Tick > Loop.CurrentTick)
            {
                _pendingHashes[packet.Tick] = packet;
                return;
            }

            VerifyStateHash(packet);
        }
        catch (Exception ex)
        {
            Log($"Error processing StateHash: {ex}");
        }
    }

    private void ProcessPendingHashes()
    {
        long currentTick = Loop.CurrentTick;

        if (_pendingHashes.TryGetValue(currentTick, out var packet))
        {
            VerifyStateHash(packet);
            _pendingHashes.Remove(currentTick);
        }

        if (_isWaitingForFullState && currentTick - _fullStateRequestTick > FullStateRetryInterval)
        {
            Log("[StateHash] Retrying full state request (no response received)...");
            _fullStateRequestTick = currentTick;
            _ = RequestFullState();
        }

        var ticksToRemove = new System.Collections.Generic.List<long>();
        foreach (var tick in _pendingHashes.Keys)
        {
            if (tick < currentTick - 300)
            {
                ticksToRemove.Add(tick);
            }
        }

        foreach (var tick in ticksToRemove)
        {
            _pendingHashes.Remove(tick);
        }
    }

    private void VerifyStateHash(StateHashPacket packet)
    {
        try
        {
            if (packet.Tick <= _hashGraceUntilTick)
            {
                Log($"[StateHash] Skipped verification at Tick {packet.Tick} (grace period until tick {_hashGraceUntilTick})");
                return;
            }

            Guid localHash;

            if (_history.TryGetSnapshotData(packet.Tick, out byte[]? snapshotData))
            {
                localHash = StateHasher.Hash(snapshotData!);
            }
            else
            {
                Log($"[StateHash] Skipped verification. Tick {packet.Tick} not in history (Current: {Loop.CurrentTick}, Oldest: {_history.GetOldestTick()}).");
                return;
            }

            if (localHash != packet.Hash)
            {
                _consecutiveMismatches++;

                FireStateMismatch(packet.Tick, (System.Guid)localHash, packet.Hash);

                if (_consecutiveMismatches >= ConsecutiveMismatchThreshold && !_isWaitingForFullState)
                {
                    StateDumper.LogMismatch("Client", packet.Tick, localHash, packet.Hash, snapshotData);
                    Log($"[StateHash] {_consecutiveMismatches} consecutive confirmed mismatches " +
                        $"at Tick {packet.Tick} — requesting full state sync...");
                    _isWaitingForFullState = true;
                    _fullStateRequestTick = Loop.CurrentTick;
                    _ = RequestFullState();
                }
                else
                {
                    Log($"[StateHash] Mismatch at Tick {packet.Tick} " +
                        $"({_consecutiveMismatches}/{ConsecutiveMismatchThreshold})");
                }
            }
            else
            {
                if (_consecutiveMismatches > 0)
                    Log($"[StateHash] Converged at Tick {packet.Tick} after {_consecutiveMismatches} mismatch(es)");
                else
                    Log($"[StateHash] Verified match at Tick {packet.Tick}");

                _consecutiveMismatches = 0;

                _unconfirmedPredictions.RemoveAll(p => p.ExecuteTick <= packet.Tick);
                if (_isWaitingForFullState)
                    _isWaitingForFullState = false;
            }
        }
        catch (Exception ex)
        {
            Log($"Error verifying StateHash: {ex}");
        }
    }

    private void OnRollbackFailed()
    {
        if (_isWaitingForFullState) return;

        Log("Rollback failed due to missing history. Requesting full state sync...");
        _isWaitingForFullState = true;
        _fullStateRequestTick = Loop.CurrentTick;
        _ = RequestFullState();
    }

    public override void Dispose()
    {
        _game.Loop.OnTick -= ProcessPendingHashes;
        base.Dispose();
    }
}
