using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Buffers;
using Deterministic.GameFramework.NetworkV2.Interfaces;
using Deterministic.GameFramework.NetworkV2.Packets;
using Deterministic.GameFramework.Reactive;
using Microsoft.Extensions.DependencyInjection;

namespace Deterministic.GameFramework.NetworkV2.Client;

public class GameClient : IDisposable, IAsyncDisposable, IActionDispatcher
{
    private readonly INetworkClient _networkClient;
    private readonly string _connectionString;
    private readonly Game _game;
    
    private System.Guid _currentMatchId;
    private readonly PacketBuffer _outgoingBuffer = new PacketBuffer();
    private readonly TaskCompletionSource _syncTcs = new TaskCompletionSource();
    private bool _isWaitingForFullState = false;
    private bool _actionsRegistered = false;
    
    // Buffering for future state hashes
    private readonly System.Collections.Generic.Dictionary<long, StateHashPacket> _pendingHashes = new();

    public event Action<string>? OnLog;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action<long, System.Guid, System.Guid>? OnStateMismatch;

    public int DefaultTickDelay { get; set; } = 5;
    public bool DefaultPrediction { get; set; } = true;

    public ReactiveSystem Reactive { get; }
    public Game Game => _game;
    public GlobalState State => _game.State;
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
        Reactive.Bind(game.State);
        
        // Hook into GameLoop
        _game.Loop.OnTick += Flush;
        _game.Loop.OnTick += ProcessPendingHashes;
        _game.Loop.OnRollbackFailed += OnRollbackFailed;
        
        _networkClient.OnTickSnapshotReceived += OnTickSnapshot;
        _networkClient.OnFullStateReceived += OnFullStateReceived;
        _networkClient.OnStateHashReceived += OnStateHashReceived;
        
        _networkClient.OnDisconnected += () => OnDisconnected?.Invoke();
        _networkClient.OnConnected += () => OnConnected?.Invoke();
    }

    private void ProcessPendingHashes()
    {
        long currentTick = Loop.CurrentTick;
        
        // Check if we have a hash for the current tick (or slightly older if we missed it)
        // We iterate keys to find match. Since it's a Dictionary, fast lookup is only possible if we know the tick.
        // But we might have multiple.
        
        if (_pendingHashes.TryGetValue(currentTick, out var packet))
        {
            VerifyStateHash(packet);
            _pendingHashes.Remove(currentTick);
        }
        
        // Cleanup old hashes (if we moved past them without verifying, e.g. rollback/reset)
        // This is O(N) but N is small.
        var ticksToRemove = new System.Collections.Generic.List<long>();
        foreach (var tick in _pendingHashes.Keys)
        {
            if (tick < currentTick - 300) // Older than history buffer
            {
                ticksToRemove.Add(tick);
            }
        }
        
        foreach (var tick in ticksToRemove)
        {
            _pendingHashes.Remove(tick);
        }
    }

    private void OnStateHashReceived(byte[] data)
    {
        try
        {
            var span = new ReadOnlySpan<byte>(data);
            if (span.Length < Marshal.SizeOf<StateHashPacket>()) return;

            var packet = MemoryMarshal.Read<StateHashPacket>(span);
            
            // If the hash is for a future tick, buffer it
            if (packet.Tick > Loop.CurrentTick)
            {
                // Log($"[StateHash] Buffering hash for future Tick {packet.Tick} (Current: {Loop.CurrentTick})");
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

    private void VerifyStateHash(StateHashPacket packet)
    {
        try
        {
            Deterministic.GameFramework.CoreV2.Guid localHash;

            // ALWAYS use history for thread safety.
            // Accessing _game.State directly is unsafe because GameLoop might be modifying it.
            if (_game.Loop.History.TryGetSnapshotData(packet.Tick, out byte[]? snapshotData))
            {
                localHash = StateHasher.Hash(snapshotData!);
            }
            else
            {
                // If it's not in history, it might be too old, or we haven't stored it yet (e.g. tick 0).
                // Safest to skip verification than to crash the networking thread.
                Log($"[StateHash] Skipped verification. Tick {packet.Tick} not in history (Current: {Loop.CurrentTick}, Oldest: {Loop.History.GetOldestTick()}).");
                return;
            }

            if (localHash != packet.Hash)
            {
                Log($"[StateHash] MISMATCH at Tick {packet.Tick}! Local: {localHash} != Server: {packet.Hash}");
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

    public async Task ConnectAsync(System.Guid matchId)
    {
        Console.WriteLine($"[GameClient] Connecting to match {matchId} with connection string '{_connectionString}'");
        try
        {
            if (_networkClient == null)
            {
                 Console.WriteLine("[GameClient] FATAL: _networkClient is null!");
                 throw new NullReferenceException("_networkClient is null");
            }

            _currentMatchId = matchId;
            Console.WriteLine("[GameClient] Calling _networkClient.ConnectAsync...");
            await _networkClient.ConnectAsync(_connectionString); 
            Console.WriteLine("[GameClient] _networkClient.ConnectAsync returned.");
            
            PlayerId = await _networkClient.JoinMatchAsync(matchId, null);
            Log($"Connected to match {matchId}. Assigned PlayerId: {PlayerId}");
            
            // Request full state on connect
            Console.WriteLine("[GameClient] Requesting full state...");
            Task.WaitAll(RequestFullState(), WaitForSyncAsync());
        }
        catch (Exception ex)
        {
            Log($"Connection error: {ex.Message}");
            Console.WriteLine($"[GameClient] Stack Trace: {ex.StackTrace}");
            throw;
        }
    }
    
    public async Task RequestFullState()
    {
         await _networkClient.RequestFullStateAsync(_currentMatchId);
    }

    public Task WaitForSyncAsync()
    {
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
        MemoryMarshal.Write(new Span<byte>(data), in action);

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
            
            MemoryMarshal.Write(span, in header);
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

    private void OnTickSnapshot(byte[] packetData)
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
    
    private void OnFullStateReceived(byte[] packetData)
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
            
            Log("Deserializing state...");
            // Provide mapper to translate Server Component IDs to Local Component IDs
            StateSerializer.Deserialize(State, stateData);
            Log("State deserialized!");
            
            Log($"Setting tick to {header.Tick}...");
            Loop.ForceSetTick(header.Tick);
            
            // Store authoritative state in history so we can verify hashes against it
            // and use it as a rollback baseline.
            Loop.History.Store(header.Tick, State);
            
            Log($"Tick set to {header.Tick}!");
            
            // Critical: Prune scheduler history to match new authoritative state
            // This resets EarliestDirtyTick and prevents immediate rollback attempts to the past
            Scheduler.PruneHistory(header.Tick);
            
            Log("Completing sync task...");
            _syncTcs.TrySetResult();
            
            _isWaitingForFullState = false;
        }
        catch (Exception ex)
        {
            Log($"Error processing Full State: {ex}");
            _syncTcs.TrySetException(ex);
            _isWaitingForFullState = false;
        }
    }

    private void Log(string msg)
    {
        OnLog?.Invoke($"[GameClient] {msg}");
    }

    public void Dispose()
    {
        Reactive.Dispose();
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
