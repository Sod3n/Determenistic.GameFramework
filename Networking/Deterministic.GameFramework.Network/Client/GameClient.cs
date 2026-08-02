using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.Debugging;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Interfaces;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Reactive;
using Deterministic.GameFramework.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Deterministic.GameFramework.Network.Client;

public class GameClient : IDisposable, IAsyncDisposable, IActionDispatcher
{
    protected readonly INetworkClient _networkClient;
    private string _connectionString;
    protected readonly Game _game;

    protected System.Guid _currentMatchId;
    public System.Guid CurrentMatchId => _currentMatchId;
    private readonly PacketBuffer _outgoingBuffer = new PacketBuffer();
    protected readonly TaskCompletionSource<bool> _syncTcs = new TaskCompletionSource<bool>();
    protected bool _isWaitingForFullState = false;
    protected long _fullStateRequestTick = 0;
    protected const long FullStateRetryInterval = 120;

    public struct PendingNetworkAction
    {
        public long ExecuteTick;
        public byte[] SerializedBytes;
    }
    private readonly List<PendingNetworkAction> _pendingActions = new();
    private long _serverMinAllowedTick = 0;

    protected struct PredictedAction
    {
        public DenseComponentId ComponentId;
        public byte[] Data;
        public int TargetEntityId;
        public long ExecuteTick;
    }
    protected readonly List<PredictedAction> _unconfirmedPredictions = new();

    private int _simulatedLatencyMs = 0;
    private readonly ConcurrentQueue<(long releaseTimestamp, byte[] data, int type)> _delayQueue = new();

    public int SimulatedLatencyMs
    {
        get => _simulatedLatencyMs;
        set => _simulatedLatencyMs = value;
    }

    protected readonly ConcurrentQueue<byte[]> _incomingFullStates = new();

    public event Action<string>? OnLog;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<long, System.Guid, System.Guid>? OnStateMismatch;

    public event Action<System.Guid>? OnLobbyCreated;
    public event Action<System.Guid>? OnLobbyJoined;
    public event Action<System.Guid>? OnMatchAssigned;

    public int DefaultTickDelay { get; set; } = 5;
    public bool DefaultPrediction { get; set; } = true;

    public ReactiveSystem Reactive { get; }
    public Game Game => _game;
    public EntityWorld State => _game.State;
    public GameLoop Loop => _game.Loop;
    public Dispatcher Dispatcher => _game.Dispatcher;
    public ActionScheduler Scheduler => _game.Scheduler;

    public string ConnectionString => _connectionString;
    public System.Guid PlayerId { get; protected set; }

    /// <summary>Auth token sent during JoinMatch. Set this to the player's persistent ID
    /// so the server's IAuthService returns it as PlayerId, allowing the same player to
    /// keep their identity across reconnects and across offline → online publish.</summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Base GameClient: no synced game loop. For server-authoritative behaviour, use a
    /// subclass that installs the matching strategy:
    /// <see cref="Deterministic.GameFramework.DeltaSync.DeltaSyncGameClient"/> or
    /// <see cref="Deterministic.GameFramework.GGPO.GGPOGameClient"/>.
    /// </summary>
    public GameClient(INetworkClient networkClient, string connectionString, Game game)
    {
        _game = game;
        _networkClient = networkClient;
        _connectionString = connectionString;

        Reactive = ReactiveSystem.Instance;
        Reactive.Dispose();
        Reactive.Bind(game.State, game.Loop);

        _game.Loop.OnBeforeTick += DrainNetworkQueues;
        _game.Loop.OnTick += Flush;
        _game.Loop.OnTick += UnpauseReactiveAfterCatchUp;

        SetupModeSpecificHandlers();

        _networkClient.OnLobbyCreated += (id) => OnLobbyCreated?.Invoke(id);
        _networkClient.OnLobbyJoined += (id) => OnLobbyJoined?.Invoke(id);
        _networkClient.OnMatchAssigned += HandleMatchAssigned;

        _networkClient.OnDisconnected += () => OnDisconnected?.Invoke();
        _networkClient.OnConnected += () => OnConnected?.Invoke();
    }

    protected virtual void SetupModeSpecificHandlers()
    {
        _networkClient.OnFullStateReceived += data => EnqueueWithDelay(data, 1);
    }

    public void SetConnectionString(string connectionString)
    {
        _connectionString = connectionString;
    }

    private void HandleMatchAssigned(System.Guid matchId)
    {
        Log($"[MatchAssignment] Assigned to match {matchId}");
        OnMatchAssigned?.Invoke(matchId);

        _ = JoinMatchInternalAsync(matchId);
    }

    private async Task JoinMatchInternalAsync(System.Guid matchId)
    {
        try
        {
            _currentMatchId = matchId;
            PlayerId = await _networkClient.JoinMatchAsync(matchId, AuthToken);
            Log($"Joined match {matchId}. Assigned PlayerId: {PlayerId}");

            Log("[GameClient] Requesting full state...");
            await RequestFullState();
        }
        catch (Exception ex)
        {
            Log($"Error joining assigned match: {ex}");
        }
    }

    private bool _latencyActive = false;

    public void ActivateSimulatedLatency() => _latencyActive = true;

    protected void EnqueueWithDelay(byte[] data, int type)
    {
        if (_simulatedLatencyMs <= 0 || !_latencyActive)
        {
            EnqueueDirect(data, type);
            return;
        }

        long releaseTime = Stopwatch.GetTimestamp()
            + (long)(_simulatedLatencyMs * 0.001 * Stopwatch.Frequency);
        _delayQueue.Enqueue((releaseTime, data, type));
    }

    protected virtual void EnqueueDirect(byte[] data, int type)
    {
        switch (type)
        {
            case 1: _incomingFullStates.Enqueue(data); break;
        }
    }

    private void FlushDelayQueue()
    {
        if (_simulatedLatencyMs <= 0) return;

        long now = Stopwatch.GetTimestamp();
        while (_delayQueue.TryPeek(out var item) && item.releaseTimestamp <= now)
        {
            _delayQueue.TryDequeue(out item);
            EnqueueDirect(item.data, item.type);
        }
    }

    public virtual void DrainNetworkQueues()
    {
        FlushDelayQueue();

        while (_incomingFullStates.TryDequeue(out var data))
        {
            ApplyFullState(data);
        }
    }

    protected virtual void ApplyFullState(byte[] packetData)
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
                    byte[] localData = StateSerializer.Serialize(State);
                    Log($"[StateDiff] Comparing live state at tick {clientTickBeforeSync} with server tick {header.Tick}");
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

            OnFullStateDeserialized(header.Tick, stateData);

            Log($"Tick set to {header.Tick}!");

            Scheduler.PruneHistory(header.Tick);

            ClearAndReAddPredictions(header.Tick);

            long serverTick = header.ServerTick > 0 ? header.ServerTick : header.Tick;

            Reactive.IsPaused = true;
            _reactiveUnpauseAtTick = clientTickBeforeSync;

            _isWaitingForFullState = false;

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

    protected void ClearAndReAddPredictions(long tick)
    {
        lock (_pendingActions)
        {
            _pendingActions.Clear();
        }
        _serverMinAllowedTick = tick;

        int removedCount = _unconfirmedPredictions.RemoveAll(p => p.ExecuteTick < tick);
        if (removedCount > 0)
            Log($"[Resync] Pruned {removedCount} old predictions (execTick < {tick})");

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
            Log($"[Resync] Re-added {reAddedCount} predicted actions (execTick >= {tick})");
    }

    protected virtual void OnFullStateDeserialized(long tick, byte[] stateData) { }

    protected void SetReactiveUnpauseAtTick(long tick)
    {
        _reactiveUnpauseAtTick = tick;
    }

    private long _reactiveUnpauseAtTick;

    private void UnpauseReactiveAfterCatchUp()
    {
        if (!Reactive.IsPaused) return;
        if (Loop.CurrentTick < _reactiveUnpauseAtTick) return;

        Reactive.IsPaused = false;
    }

    public async Task ConnectAsync()
    {
        ILogger.Log($"[GameClient] Connecting to server at '{_connectionString}'");
        if (_networkClient == null) throw new NullReferenceException("_networkClient is null");

        await _networkClient.ConnectAsync(_connectionString);
        ILogger.Log("[GameClient] Connected to gateway.");
    }

    public async Task EnqueuePlayerAsync()
    {
        await _networkClient.EnqueuePlayerAsync();
    }

    public async Task CreateLobbyAsync(string lobbyName)
    {
        await _networkClient.CreateLobbyAsync(lobbyName);
    }

    public async Task JoinLobbyAsync(System.Guid lobbyId)
    {
        await _networkClient.JoinLobbyAsync(lobbyId);
    }

    public async Task StartLobbyMatchAsync(System.Guid lobbyId, byte[]? initialState = null)
    {
        await _networkClient.StartLobbyMatchAsync(lobbyId, initialState);
    }

    public async Task ConnectAsync(System.Guid matchId)
    {
        await ConnectAsync();
        await JoinMatchInternalAsync(matchId);
        await WaitForSyncAsync();
    }

    public async Task RequestFullState()
    {
        await _networkClient.RequestFullStateAsync(_currentMatchId);
    }

    public Task WaitForSyncAsync()
    {
        var spinWait = new System.Threading.SpinWait();
        while (!_syncTcs.Task.IsCompleted)
        {
            DrainNetworkQueues();
            spinWait.SpinOnce();
        }
        return _syncTcs.Task;
    }

    public void Execute<TAction>(TAction action, int targetEntityId, int? tickDelay = null, bool? predict = null) where TAction : struct, IAction
    {
        int actualDelay = tickDelay ?? DefaultTickDelay;
        bool actualPredict = predict ?? DefaultPrediction;

        var componentId = ComponentId<TAction>.DenseId;
        long baseTick = GetBaseTickForExecute();
        long executeTick = baseTick + actualDelay;

        int size = Marshal.SizeOf<TAction>();
        byte[] data = new byte[size];
        MemoryMarshal.Write(new Span<byte>(data), ref action);

        long predictionId = actualPredict ? GetPredictionId() : 0;

        if (actualPredict)
        {
            var result = Scheduler.Schedule(action, componentId, new Entity(targetEntityId), executeTick, predictionId, out long actualTick);
            if (actualTick != executeTick)
                Log($"[Prediction] {typeof(TAction).Name} target={targetEntityId} execTick={executeTick}->{actualTick} pid={predictionId} (local bump) result={result} (clientTick={Loop.CurrentTick})");
            else
                Log($"[Prediction] {typeof(TAction).Name} target={targetEntityId} execTick={executeTick} pid={predictionId} result={result} (clientTick={Loop.CurrentTick})");

            executeTick = actualTick;

            if (result == ActionScheduler.ScheduleResult.Duplicate)
            {
                return;
            }

            _unconfirmedPredictions.Add(new PredictedAction
            {
                ComponentId = componentId,
                Data = (byte[])data.Clone(),
                TargetEntityId = targetEntityId,
                ExecuteTick = executeTick
            });
        }

        _ = SendAction(componentId, data, targetEntityId, executeTick, predictionId);
    }

    protected virtual long GetBaseTickForExecute() => Loop.CurrentTick;

    protected virtual long GetPredictionId() => 0;

    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Execute(action, target.Id);
    }

    public Task SendAction(DenseComponentId componentId, byte[] data, int targetEntityId, long tick, long predictionId = 0)
    {
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        byte[] serialized = new byte[headerSize + data.Length];

        var header = new NetworkActionHeader
        {
            ComponentId = componentId,
            TargetEntityId = targetEntityId,
            ExecuteTick = tick,
            PredictionId = predictionId,
            DataLength = data.Length
        };

        MemoryMarshal.Write(new Span<byte>(serialized), ref header);
        data.CopyTo(serialized.AsSpan(headerSize));

        lock (_pendingActions)
        {
            _pendingActions.Add(new PendingNetworkAction
            {
                ExecuteTick = tick,
                SerializedBytes = serialized
            });
        }

        return Task.CompletedTask;
    }

    private void Flush()
    {
        lock (_pendingActions)
        {
            if (_pendingActions.Count == 0) return;

            FlushPendingActions(_pendingActions);

            if (_pendingActions.Count == 0) return;

            _outgoingBuffer.Reset();
            foreach (var pending in _pendingActions)
            {
                var span = _outgoingBuffer.GetSpan(pending.SerializedBytes.Length);
                pending.SerializedBytes.CopyTo(span);
                _outgoingBuffer.Advance(pending.SerializedBytes.Length);
            }

            byte[] payload = _outgoingBuffer.ToArray();
            _networkClient.SendBatch(payload);
        }
    }

    protected virtual void FlushPendingActions(List<PendingNetworkAction> pending)
    {
    }

    protected void Log(string msg)
    {
        OnLog?.Invoke($"[GameClient] {msg}");
    }

    protected void FireStateMismatch(long tick, System.Guid localHash, System.Guid serverHash)
    {
        OnStateMismatch?.Invoke(tick, localHash, serverHash);
    }

    public virtual void Dispose()
    {
        Reactive.Dispose();
        Loop.OnBeforeTick -= DrainNetworkQueues;
        Loop.OnTick -= Flush;
        _ = DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_networkClient != null)
        {
            await _networkClient.DisposeAsync();
        }
    }
}
