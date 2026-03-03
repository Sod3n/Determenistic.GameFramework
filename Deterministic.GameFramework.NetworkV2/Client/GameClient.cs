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
    private readonly Dispatcher _dispatcher;
    private readonly GlobalState _state;
    private readonly ActionScheduler _scheduler;
    private readonly GameLoop _gameLoop;
    
    private System.Guid _currentMatchId;
    private readonly PacketBuffer _outgoingBuffer = new PacketBuffer();
    private readonly TaskCompletionSource _syncTcs = new TaskCompletionSource();
    private bool _isWaitingForFullState = false;
    
    public event Action<string>? OnLog;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public int DefaultTickDelay { get; set; } = 5;
    public bool DefaultPrediction { get; set; } = true;

    public ReactiveSystem Reactive { get; }
    public GlobalState State => _state;
    
    public System.Guid PlayerId { get; private set; }

    public GameClient(INetworkClient networkClient, string connectionString, GlobalState state, Dispatcher dispatcher, ActionScheduler scheduler, GameLoop gameLoop)
    {
        _state = state;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
        _gameLoop = gameLoop;
        _networkClient = networkClient;
        _connectionString = connectionString;
        
        Reactive = ReactiveSystem.Instance;
        // Since ReactiveSystem is a singleton, ensure we start with a clean slate
        Reactive.Dispose(); 
        Reactive.Bind(state);
        
        // Auto-discover services and NetworkIds
        ServiceLocator.Initialize(dispatcher);
        ServiceLocator.Initialize(gameLoop);
        
        // Hook into GameLoop
        _gameLoop.OnTick += Flush;
        _gameLoop.OnRollbackFailed += OnRollbackFailed;
        
        _networkClient.OnTickSnapshotReceived += OnTickSnapshot;
        _networkClient.OnFullStateReceived += OnFullStateReceived;
        
        _networkClient.OnDisconnected += () => OnDisconnected?.Invoke();
        _networkClient.OnConnected += () => OnConnected?.Invoke();
    }

    public Context CreateContext(Entity entity) => new Context(_state, entity, this);

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
            
            // Note: SignalR adapter might fire OnConnected immediately if already started, 
            // but LiteNetLib needs explicit connect. 
            // The INetworkClient.ConnectAsync should handle the transport connection.
            
            // Wait a bit for connection if needed or rely on event? 
            // For now assume ConnectAsync establishes link.
            
            Console.WriteLine("[GameClient] Joining match...");
            PlayerId = await _networkClient.JoinMatchAsync(matchId, null);
            Log($"Connected to match {matchId}. Assigned PlayerId: {PlayerId}");
            
            // Request full state on connect
            Console.WriteLine("[GameClient] Requesting full state...");
            await RequestFullState();
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

        int denseId = _dispatcher.GetDenseId<TAction>();
        long executeTick = _gameLoop.CurrentTick + actualDelay;

        // Serialize
        int size = Marshal.SizeOf<TAction>();
        byte[] data = new byte[size];
        MemoryMarshal.Write(new Span<byte>(data), in action);

        // Schedule Locally (Prediction)
        if (actualPredict)
        {
            var result = _scheduler.Schedule(action, denseId, new Entity(targetEntityId), executeTick);
            if (result == ActionScheduler.ScheduleResult.Duplicate)
            {
                Log($"[Prediction] Duplicate action {typeof(TAction).Name} ignored.");
                return;
            }
        }

        // Send to Server
        _ = SendAction(denseId, data, targetEntityId, executeTick);
    }

    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Execute(action, target.Id);
    }

    public Task SendAction(int denseId, byte[] data, int targetEntityId, long tick)
    {
        // Buffer the action
        lock (_outgoingBuffer)
        {
            int headerSize = Marshal.SizeOf<NetworkActionHeader>();
            int totalSize = headerSize + data.Length;
            
            var span = _outgoingBuffer.GetSpan(totalSize);
            var header = new NetworkActionHeader
            {
                DenseId = denseId,
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
            
            _scheduler.ScheduleFromBytes(actionHeader.DenseId, dataSpan, actionHeader.TargetEntityId, actionHeader.ExecuteTick);
        }
        
        // 2. Sync Tick (Basic)
        long delta = header.ServerTick - _gameLoop.CurrentTick;
        if (Math.Abs(delta) > 60) // 1 second drift
        {
            Log($"Tick Drift Large: {delta}. Server: {header.ServerTick}, Client: {_gameLoop.CurrentTick}");
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
            StateSerializer.Deserialize(_state, stateData);
            Log("State deserialized!");
            
            Log($"Setting tick to {header.Tick}...");
            _gameLoop.ForceSetTick(header.Tick);
            Log($"Tick set to {header.Tick}!");
            
            // Critical: Prune scheduler history to match new authoritative state
            // This resets EarliestDirtyTick and prevents immediate rollback attempts to the past
            _scheduler.PruneHistory(header.Tick);
            
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
        _gameLoop.OnTick -= Flush;
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
