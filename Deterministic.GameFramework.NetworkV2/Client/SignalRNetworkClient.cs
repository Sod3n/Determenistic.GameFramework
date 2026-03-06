using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Deterministic.GameFramework.NetworkV2.Interfaces;

namespace Deterministic.GameFramework.NetworkV2.Client;

public class SignalRNetworkClient : INetworkClient
{
    private readonly HubConnection _hubConnection;
    private bool _isConnected;
    private TaskCompletionSource<Guid>? _joinMatchTcs;

    public event Action<byte[]>? OnTickSnapshotReceived;
    public event Action<byte[]>? OnFullStateReceived;
    public event Action<byte[]>? OnComponentMappingReceived;
    public event Action<byte[]>? OnStateHashReceived;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    
    public int Ping => 0; // SignalR doesn't expose RTT easily without custom ping

    public SignalRNetworkClient(string serverUrl)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(serverUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<byte[]>("OnTickSnapshot", data => OnTickSnapshotReceived?.Invoke(data));
        _hubConnection.On<byte[]>("OnFullStateReceived", data => OnFullStateReceived?.Invoke(data));
        _hubConnection.On<byte[]>("OnComponentMappingReceived", data => OnComponentMappingReceived?.Invoke(data));
        _hubConnection.On<byte[]>("OnStateHashReceived", data => OnStateHashReceived?.Invoke(data));
        _hubConnection.On<byte[]>("OnMatchJoined", data => 
        {
            if (data.Length >= 16)
            {
                var guid = new Guid(data.AsSpan(0, 16));
                _joinMatchTcs?.TrySetResult(guid);
            }
        });
        
        _hubConnection.Closed += (arg) => 
        {
            _isConnected = false;
            OnDisconnected?.Invoke();
            return Task.CompletedTask;
        };
        
        _hubConnection.Reconnected += (arg) => 
        {
            _isConnected = true;
            OnConnected?.Invoke();
            return Task.CompletedTask;
        };
    }

    public async Task ConnectAsync(string? address = null)
    {
        // address arg is ignored if configured in ctor, but interface requires it.
        // In this adapter we assume ctor url or ignore.
        if (_isConnected) return;
        
        await _hubConnection.StartAsync();
        _isConnected = true;
        OnConnected?.Invoke();
    }
    
    public Task<Guid> JoinMatchAsync(Guid matchId, string? token = null)
    {
        if (!_isConnected) return Task.FromException<Guid>(new InvalidOperationException("Not connected"));
        
        _joinMatchTcs = new TaskCompletionSource<Guid>();
        // We still await the Invoke, but we return the TCS task for the result
        // Use fire-and-forget for the invoke to avoid blocking on it if it doesn't return the value directly
        // But InvokeAsync waits for server method completion.
        
        var invokeTask = _hubConnection.InvokeAsync("JoinMatch", matchId, token);
        
        // Return the TCS task which completes when "OnMatchJoined" is received
        return _joinMatchTcs.Task;
    }

    public async Task RequestFullStateAsync(Guid matchId)
    {
        if (!_isConnected) return;
        await _hubConnection.InvokeAsync("RequestFullState", matchId);
    }

    public void SendBatch(byte[] data)
    {
        if (!_isConnected) return;
        // Fire and forget
        _ = _hubConnection.InvokeAsync("SendBatch", data);
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
    }
}
