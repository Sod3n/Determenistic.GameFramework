using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Interfaces;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Reactive;
using Microsoft.Extensions.DependencyInjection;

namespace Deterministic.GameFramework.Network.Client;

public class GameClient : IDisposable, IAsyncDisposable, IActionDispatcher
{
    private readonly INetworkClient _networkClient;
    private readonly string _connectionString;
    private readonly Game _game;

    private System.Guid _currentMatchId;
    private readonly PacketBuffer _outgoingBuffer = new PacketBuffer();
    private readonly TaskCompletionSource<bool> _syncTcs = new TaskCompletionSource<bool>();
    private bool _isWaitingForFullState = false;

    // Buffering for future state hashes
    private readonly System.Collections.Generic.Dictionary<long, StateHashPacket> _pendingHashes = new();

    // ── Inbound queues: network thread enqueues, game loop thread dequeues ──
    private readonly ConcurrentQueue<byte[]> _incomingTickSnapshots = new();
    private readonly ConcurrentQueue<byte[]> _incomingFullStates = new();
    private readonly ConcurrentQueue<byte[]> _incomingStateHashes = new();

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

    public System.Guid PlayerId { get; private set; }

    public GameClient(INetworkClient networkClient, string connectionString, Game game)
    {
        _game = game;
        _networkClient = networkClient;
        _connectionString = connectionString;

        Reactive = ReactiveSystem.Instance;
        // Since ReactiveSystem is a singleton, ensure we start with a clean slate
        Reactive.Dispose();
        Reactive.Bind(game.State, game.Loop);

        // Hook into GameLoop — drain inbound queues before each tick
        _game.Loop.OnBeforeTick += DrainNetworkQueues;
        _game.Loop.OnTick += Flush;
        _game.Loop.OnTick += ProcessPendingHashes;
        _game.Loop.OnRollbackFailed += OnRollbackFailed;

        // Network callbacks just enqueue raw bytes — never touch game state
        _networkClient.OnTickSnapshotReceived += data => _incomingTickSnapshots.Enqueue(data);
        _networkClient.OnFullStateReceived += data => _incomingFullStates.Enqueue(data);
        _networkClient.OnStateHashReceived += data => _incomingStateHashes.Enqueue(data);

        _networkClient.OnLobbyCreated += (id) => OnLobbyCreated?.Invoke(id);
        _networkClient.OnLobbyJoined += (id) => OnLobbyJoined?.Invoke(id);
        _networkClient.OnMatchAssigned += HandleMatchAssigned;

        _networkClient.OnDisconnected += () => OnDisconnected?.Invoke();
        _networkClient.OnConnected += () => OnConnected?.Invoke();
    }

    private void HandleMatchAssigned(System.Guid matchId)
    {
        Log($"[MatchAssignment] Assigned to match {matchId}");
        OnMatchAssigned?.Invoke(matchId);

        // Auto-join the assigned match
        _ = JoinMatchInternalAsync(matchId);
    }

    private async Task JoinMatchInternalAsync(System.Guid matchId)
    {
        try
        {
            _currentMatchId = matchId;
            PlayerId = await _networkClient.JoinMatchAsync(matchId, null);
            Log($"Joined match {matchId}. Assigned PlayerId: {PlayerId}");

            Log("[GameClient] Requesting full state...");
            await RequestFullState();
        }
        catch (Exception ex)
        {
            Log($"Error joining assigned match: {ex}");
        }
    }

    // ── Called on the game loop thread before each tick (via OnBeforeTick). ──
    // Also called from WaitForSyncAsync to apply the initial full state before the loop starts.
    public void DrainNetworkQueues()
    {
        // 1. Apply full state (if any) — this resets everything, so do it first
        while (_incomingFullStates.TryDequeue(out var data))
        {
            ApplyFullState(data);
        }

        // 2. Process tick snapshots (schedule actions)
        while (_incomingTickSnapshots.TryDequeue(out var data))
        {
            ApplyTickSnapshot(data);
        }

        // 3. Buffer state hashes
        while (_incomingStateHashes.TryDequeue(out var data))
        {
            ApplyStateHash(data);
        }
    }

    private void ApplyTickSnapshot(byte[] packetData)
    {
        // Parse Header
        var packetSpan = new ReadOnlySpan<byte>(packetData);
        int headerSize = Marshal.SizeOf<TickSnapshotHeader>();

        if (packetSpan.Length < headerSize) return; // Invalid

        var header = MemoryMarshal.Read<TickSnapshotHeader>(packetSpan);
        var payloadSpan = packetSpan.Slice(headerSize, header.PayloadLength);

        Log($"Received TickSnapshot: ServerTick={header.ServerTick}, PayloadLength={header.PayloadLength}");

        // 1. Process Actions from Binary Payload
        int offset = 0;
        int actionHeaderSize = Marshal.SizeOf<NetworkActionHeader>();

        while (offset + actionHeaderSize <= payloadSpan.Length)
        {
            var actionHeader = MemoryMarshal.Read<NetworkActionHeader>(payloadSpan.Slice(offset));
            offset += actionHeaderSize;

            if (offset + actionHeader.DataLength > payloadSpan.Length) break; // Malformed

            var dataSpan = payloadSpan.Slice(offset, actionHeader.DataLength);
            offset += actionHeader.DataLength;

            Scheduler.ScheduleFromBytes(actionHeader.ComponentId, dataSpan, actionHeader.TargetEntityId, actionHeader.ExecuteTick);
        }

        // 2. Sync Tick (Basic)
        long delta = header.ServerTick - Loop.CurrentTick;
        if (Math.Abs(delta) > 60) // 1 second drift
        {
            Log($"Tick Drift Large: {delta}. Server: {header.ServerTick}, Client: {Loop.CurrentTick}");
        }
    }

    private void ApplyFullState(byte[] packetData)
    {
        try
        {
            // Parse Header
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

            // If this is a resync (not initial sync), diff local vs server state
            if (_isWaitingForFullState)
            {
                try
                {
                    byte[] localData = StateSerializer.Serialize(State);
                    StateDumper.LogStateDiff("Client", header.Tick, localData, stateData);
                }
                catch (Exception ex)
                {
                    Log($"[StateDiff] Failed to diff states: {ex.Message}");
                }
            }

            // Adopt server's component ID mappings on every full state sync.
            // The server state may contain newly active components (e.g. HouseComponent
            // after building, EnterStateComponent during taming) whose LocalIds the
            // client hasn't seen yet. Overlaying is safe — it only updates LocalIds
            // for known StableIds without clearing existing registrations.
            StateSerializer.AdoptMappingsFrom(stateData);

            Log("Deserializing state...");
            StateSerializer.Deserialize(State, stateData, syncComponentIds: false);
            Log("State deserialized!");

            Log($"Setting tick to {header.Tick}...");
            Loop.ForceSetTick(header.Tick);

            // Store authoritative state in history
            Loop.Simulation.History.Store(header.Tick, State);

            Log($"Tick set to {header.Tick}!");

            // Critical: Prune scheduler history to match new authoritative state
            Scheduler.PruneHistory(header.Tick);

            // Critical: Reset Reactive System to re-scan the world.
            Reactive.Reset();

            Log("Completing sync task...");
            _syncTcs.TrySetResult(true);

            _isWaitingForFullState = false;
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

            // If the hash is for a future tick, buffer it
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

        // Cleanup old hashes
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
            Guid localHash;

            // ALWAYS use history for thread safety.
            if (_game.Simulation.History.TryGetSnapshotData(packet.Tick, out byte[]? snapshotData))
            {
                localHash = StateHasher.Hash(snapshotData!);
            }
            else
            {
                Log($"[StateHash] Skipped verification. Tick {packet.Tick} not in history (Current: {Loop.CurrentTick}, Oldest: {Loop.Simulation.History.GetOldestTick()}).");
                return;
            }

            if (localHash != packet.Hash)
            {
                StateDumper.LogMismatch("Client", packet.Tick, localHash, packet.Hash, snapshotData);

                OnStateMismatch?.Invoke(packet.Tick, (System.Guid)localHash, packet.Hash);

                if (!_isWaitingForFullState)
                {
                    Log("[StateHash] Requesting full state sync due to mismatch...");
                    _isWaitingForFullState = true;
                    _ = RequestFullState();
                }
            }
            else
            {
                Log($"[StateHash] Verified match at Tick {packet.Tick}");
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
        _ = RequestFullState();
    }

    public async Task ConnectAsync()
    {
        Console.WriteLine($"[GameClient] Connecting to server at '{_connectionString}'");
        if (_networkClient == null) throw new NullReferenceException("_networkClient is null");

        await _networkClient.ConnectAsync(_connectionString);
        Console.WriteLine("[GameClient] Connected to gateway.");
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
        // The initial full state arrives on the network thread into the queue.
        // Spin-drain it here (game loop hasn't started yet) until the sync completes.
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
        long executeTick = Loop.CurrentTick + actualDelay;

        // Serialize
        int size = Marshal.SizeOf<TAction>();
        byte[] data = new byte[size];
        MemoryMarshal.Write(new Span<byte>(data), ref action);

        // Schedule Locally (Prediction)
        if (actualPredict)
        {
            var result = Scheduler.Schedule(action, componentId, new Entity(targetEntityId), executeTick);
            if (result == ActionScheduler.ScheduleResult.Duplicate)
            {
                Log($"[Prediction] Duplicate action {typeof(TAction).Name} ignored.");
                return;
            }
        }

        // Send to Server
        _ = SendAction(componentId, data, targetEntityId, executeTick);
    }

    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Execute(action, target.Id);
    }

    public Task SendAction(DenseComponentId componentId, byte[] data, int targetEntityId, long tick)
    {
        // Buffer the action
        lock (_outgoingBuffer)
        {
            int headerSize = Marshal.SizeOf<NetworkActionHeader>();
            int totalSize = headerSize + data.Length;

            var span = _outgoingBuffer.GetSpan(totalSize);
            var header = new NetworkActionHeader
            {
                ComponentId = componentId,
                TargetEntityId = targetEntityId,
                ExecuteTick = tick,
                DataLength = data.Length
            };

            MemoryMarshal.Write(span, ref header);
            data.CopyTo(span.Slice(headerSize));

            _outgoingBuffer.Advance(totalSize);
        }

        // Note: Actual network send happens in Flush()
        return Task.CompletedTask;
    }

    private void Flush()
    {
        byte[]? payload = null;
        lock (_outgoingBuffer)
        {
            if (_outgoingBuffer.Length > 0)
            {
                payload = _outgoingBuffer.ToArray();
                _outgoingBuffer.Reset();
            }
        }

        if (payload != null)
        {
            _networkClient.SendBatch(payload);
        }
    }

    private void Log(string msg)
    {
        OnLog?.Invoke($"[GameClient] {msg}");
    }

    public void Dispose()
    {
        Reactive.Dispose();
        Loop.OnBeforeTick -= DrainNetworkQueues;
        Loop.OnTick -= Flush;
        Loop.OnTick -= ProcessPendingHashes;
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
