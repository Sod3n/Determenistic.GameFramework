using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using Deterministic.GameFramework.NetworkV2.Interfaces;
using Deterministic.GameFramework.NetworkV2.Packets;

namespace Deterministic.GameFramework.NetworkV2.Client;

public class LiteNetLibNetworkClient : INetworkClient, INetEventListener
{
    private readonly NetManager _netManager;
    private NetPeer? _peer;
    private TaskCompletionSource<bool>? _connectTcs;
    private TaskCompletionSource<Guid>? _joinMatchTcs;
    
    public event Action<byte[]>? OnTickSnapshotReceived;
    public event Action<byte[]>? OnFullStateReceived;
    public event Action<byte[]>? OnComponentMappingReceived;
    public event Action<byte[]>? OnStateHashReceived;
    public event Action? OnConnected;
    public event Action? OnDisconnected;

    public int Ping => _peer?.Ping ?? -1;
    public bool IsConnected => _peer != null && _peer.ConnectionState == ConnectionState.Connected;

    public LiteNetLibNetworkClient()
    {
        _netManager = new NetManager(this);
        _netManager.UpdateTime = 15; // Poll every 15ms
    }

    public Task ConnectAsync(string address)
    {
        Console.WriteLine($"[LiteNetLibClient] ConnectAsync called with address: '{address}'");
        
        if (_netManager == null)
        {
             Console.WriteLine("[LiteNetLibClient] FATAL: _netManager is null!");
             throw new NullReferenceException("_netManager is null");
        }

        if (_netManager.IsRunning) _netManager.Stop();
        
        _connectTcs = new TaskCompletionSource<bool>();
        _netManager.Start();
        
        if (address == null)
        {
             Console.WriteLine("[LiteNetLibClient] FATAL: address is null!");
             throw new NullReferenceException("Address is null");
        }

        // Parse address "ip:port"
        string[] parts = address.Split(':');
        if (parts.Length < 2) 
        {
             _connectTcs.SetException(new ArgumentException($"Address must be in ip:port format. Got: {address}"));
             return _connectTcs.Task;
        }

        string ip = parts[0];
        if (!int.TryParse(parts[1], out int port))
        {
             _connectTcs.SetException(new ArgumentException($"Invalid port in address: {address}"));
             return _connectTcs.Task;
        }
        
        Console.WriteLine($"[LiteNetLibClient] Connecting to {ip}:{port}...");
        _peer = _netManager.Connect(ip, port, "GameKey");
        
        if (_peer == null)
        {
             Console.WriteLine("[LiteNetLibClient] FATAL: _netManager.Connect returned null!");
        }
        
        // Start polling loop
        _ = Task.Run(async () =>
        {
            Console.WriteLine("[LiteNetLibClient] Polling loop started.");
            while (_netManager.IsRunning)
            {
                _netManager.PollEvents();
                await Task.Delay(15);
            }
            Console.WriteLine("[LiteNetLibClient] Polling loop ended.");
        });

        return _connectTcs.Task;
    }

    public Task<Guid> JoinMatchAsync(Guid matchId, string? token = null)
    {
        if (_peer == null || !IsConnected) return Task.FromException<Guid>(new InvalidOperationException("Not connected"));

        _joinMatchTcs = new TaskCompletionSource<Guid>();

        var writer = new NetDataWriter();
        writer.Put((byte)PacketType.JoinMatch);
        writer.Put(matchId.ToByteArray());
        writer.Put(token ?? string.Empty);
        
        _peer.Send(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
        return _joinMatchTcs.Task;
    }

    public Task RequestFullStateAsync(Guid matchId)
    {
        if (_peer == null) return Task.CompletedTask;

        var writer = new NetDataWriter();
        writer.Put((byte)PacketType.RequestFullState);
        writer.Put(matchId.ToByteArray());
        
        _peer.Send(writer, LiteNetLib.DeliveryMethod.ReliableOrdered);
        return Task.CompletedTask;
    }

    public void SendBatch(byte[] data)
    {
        if (_peer == null) return;
        
        var writer = new NetDataWriter();
        writer.Put((byte)PacketType.Batch);
        writer.Put(data);
        
        _peer.Send(writer, LiteNetLib.DeliveryMethod.ReliableUnordered);
    }

    public ValueTask DisposeAsync()
    {
        _netManager.Stop();
        return ValueTask.CompletedTask;
    }

    // INetEventListener Implementation
    public void OnPeerConnected(NetPeer peer)
    {
        _peer = peer;
        _connectTcs?.TrySetResult(true);
        OnConnected?.Invoke();
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        _peer = null;
        if (_connectTcs != null && !_connectTcs.Task.IsCompleted)
        {
            _connectTcs.TrySetException(new Exception($"Connection failed: {disconnectInfo.Reason}"));
        }
        if (_joinMatchTcs != null && !_joinMatchTcs.Task.IsCompleted)
        {
            _joinMatchTcs.TrySetException(new Exception($"Disconnected while joining match: {disconnectInfo.Reason}"));
        }
        OnDisconnected?.Invoke();
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
    {
        Console.WriteLine($"[LiteNetLib] Network Error: {socketError}");
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, LiteNetLib.DeliveryMethod deliveryMethod)
    {
        if (reader.AvailableBytes == 0) return;

        try 
        {
            byte typeByte = reader.GetByte();
            // Check if valid enum
            if (!Enum.IsDefined(typeof(PacketType), typeByte)) return;
            
            PacketType type = (PacketType)typeByte;
            
            switch (type)
            {
                case PacketType.TickSnapshot:
                {
                    // Read remaining bytes as snapshot
                    byte[] data = reader.GetRemainingBytes();
                    OnTickSnapshotReceived?.Invoke(data);
                    break;
                }
                case PacketType.FullState:
                {
                    byte[] data = reader.GetRemainingBytes();
                    OnFullStateReceived?.Invoke(data);
                    break;
                }
                case PacketType.MatchJoined:
                {
                    byte[] guidBytes = new byte[16];
                    reader.GetBytes(guidBytes, 16);
                    var playerId = new Guid(guidBytes);
                    _joinMatchTcs?.TrySetResult(playerId);
                    break;
                }
                case PacketType.ComponentMapping:
                {
                    byte[] data = reader.GetRemainingBytes();
                    OnComponentMappingReceived?.Invoke(data);
                    break;
                }
                case PacketType.StateHash:
                {
                    byte[] data = reader.GetRemainingBytes();
                    OnStateHashReceived?.Invoke(data);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiteNetLib] Parse Error: {ex.Message}");
        }
        finally
        {
            reader.Recycle();
        }
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnConnectionRequest(ConnectionRequest request) { }
}
