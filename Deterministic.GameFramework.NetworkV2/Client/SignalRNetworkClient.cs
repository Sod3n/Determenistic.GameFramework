using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Deterministic.GameFramework.NetworkV2.Interfaces;

namespace Deterministic.GameFramework.NetworkV2.Client;

public class SignalRNetworkClient : INetworkClient
{
    private readonly HubConnection _hubConnection;
    private bool _isConnected;

    public event Action<byte[]>? OnTickSnapshotReceived;
    public event Action<byte[]>? OnFullStateReceived;
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
    
    public async Task JoinMatchAsync(Guid matchId, string? token = null)
    {
        if (!_isConnected) throw new InvalidOperationException("Not connected");
        await _hubConnection.InvokeAsync("JoinMatch", matchId, token);
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
