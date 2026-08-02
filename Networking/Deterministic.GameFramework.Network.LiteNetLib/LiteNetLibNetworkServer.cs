using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Utils.Logging;
using LiteNetLib;
using LiteNetLib.Utils;

namespace Deterministic.GameFramework.Network.Server;

public class LiteNetLibPeer : INetworkPeer
{
    private readonly NetPeer _peer;
    public string Id => _peer.Id.ToString();
    public NetPeer Peer => _peer;

    public LiteNetLibPeer(NetPeer peer)
    {
        _peer = peer;
    }

    public Task SendAsync(byte[] data, PacketType type)
    {
        var writer = new NetDataWriter();
        writer.Put((byte)type);
        writer.Put(data);
        
        var deliveryMethod = DeliveryMethod.ReliableOrdered;
        if (type == PacketType.TickSnapshot)
        {
            deliveryMethod = DeliveryMethod.ReliableOrdered;
        }
        else if (type == PacketType.StateHash)
        {
            deliveryMethod = DeliveryMethod.Sequenced;
        }
        
        _peer.Send(writer, deliveryMethod);
        return Task.CompletedTask;
    }
}

public class LiteNetLibNetworkServer : INetworkServer, IPooledBroadcastNetworkServer, INetEventListener, IDisposable
{
    private readonly NetManager _netManager;
    private readonly ConcurrentDictionary<int, LiteNetLibPeer> _peers = new();
    private readonly ConcurrentDictionary<string, HashSet<int>> _groups = new();
    private readonly object _groupLock = new();

    // Shared scratch writer for broadcasts. LiteNetLib's NetPeer.Send(NetDataWriter,...) copies
    // the writer's data into its internal send queue synchronously, so a single instance can be
    // safely reused across ticks as long as access is serialized (hence _broadcastLock). This
    // eliminates the per-broadcast `new NetDataWriter()` allocation that previously fired once
    // per match per tick.
    private readonly NetDataWriter _broadcastWriter = new();
    private readonly object _broadcastLock = new();

    // Delegate for processing incoming packets
    private readonly Func<INetworkPeer, byte[], Task> _packetHandler;
    private readonly Func<INetworkPeer, Task> _connectHandler;
    private readonly Func<INetworkPeer, Task> _disconnectHandler;

    public LiteNetLibNetworkServer(
        int port, 
        Func<INetworkPeer, byte[], Task> packetHandler,
        Func<INetworkPeer, Task> connectHandler,
        Func<INetworkPeer, Task> disconnectHandler)
    {
        _packetHandler = packetHandler;
        _connectHandler = connectHandler;
        _disconnectHandler = disconnectHandler;
        
        _netManager = new NetManager(this);
        _netManager.UnconnectedMessagesEnabled = true;
        _netManager.BroadcastReceiveEnabled = true;
        ILogger.Log($"[LiteNetLibServer] Starting on port {port}...");
        bool started = _netManager.Start(port);
        ILogger.Log($"[LiteNetLibServer] Start result: {started}");

        if (!started)
        {
            ILogger.LogError($"[LiteNetLibServer] FAILED TO START ON PORT {port}. Check permissions or if port is in use.");
        }

        // Polling loop
        _ = Task.Run(async () =>
        {
            ILogger.Log("[LiteNetLibServer] Polling loop started.");
            while (_netManager.IsRunning)
            {
                _netManager.PollEvents();
                await Task.Delay(15);
            }
            ILogger.Log("[LiteNetLibServer] Polling loop ended.");
        });
    }

    public Task JoinGroupAsync(INetworkPeer peer, string groupName)
    {
        if (peer is LiteNetLibPeer lnlPeer)
        {
            ILogger.Log($"[Server] JoinGroup {groupName} add peer {lnlPeer.Peer.Id}");
            lock (_groupLock)
            {
                if (!_groups.TryGetValue(groupName, out var set))
                {
                    set = new HashSet<int>();
                    _groups[groupName] = set;
                }
                set.Add(lnlPeer.Peer.Id);
            }
        }
        return Task.CompletedTask;
    }

    public Task LeaveGroupAsync(INetworkPeer peer, string groupName)
    {
        if (peer is LiteNetLibPeer lnlPeer)
        {
            lock (_groupLock)
            {
                if (_groups.TryGetValue(groupName, out var set))
                {
                    set.Remove(lnlPeer.Peer.Id);
                    if (set.Count == 0) _groups.TryRemove(groupName, out _);
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task BroadcastToGroupAsync(string groupName, byte[] data, PacketType type)
    {
        return BroadcastInternal(groupName, data, 0, data.Length, type);
    }

    /// <summary>
    /// Pooled-buffer broadcast path used by the delta hot loop. The caller passes a
    /// rented <paramref name="rentedBuffer"/> with valid bytes in <c>[0, length)</c>;
    /// once LiteNetLib has copied the bytes into its send queue (synchronous), the
    /// buffer is returned to <see cref="ArrayPool{T}.Shared"/>. Safe because
    /// <see cref="NetPeer.Send(NetDataWriter, DeliveryMethod)"/> is synchronous — it
    /// does not retain a reference to our bytes beyond the call.
    /// </summary>
    public Task BroadcastToGroupPooledAsync(string groupName, byte[] rentedBuffer, int length, PacketType type)
    {
        try
        {
            return BroadcastInternal(groupName, rentedBuffer, 0, length, type);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    private Task BroadcastInternal(string groupName, byte[] data, int offset, int length, PacketType type)
    {
        List<int> peerIds;
        lock (_groupLock)
        {
            if (!_groups.TryGetValue(groupName, out var set)) return Task.CompletedTask;
            peerIds = new List<int>(set);
        }
        ILogger.Log($"[Server] BroadcastToGroup {groupName}, peers={peerIds.Count}");

        var deliveryMethod = DeliveryMethod.ReliableOrdered;
        if (type == PacketType.TickSnapshot)
        {
            deliveryMethod = DeliveryMethod.ReliableOrdered;
        }
        else if (type == PacketType.StateHash)
        {
            deliveryMethod = DeliveryMethod.Sequenced;
        }

        // Serialize access to the shared writer. All sends are synchronous, so the critical
        // section is bounded by the per-peer Send calls — no awaits, no reentrancy risk.
        lock (_broadcastLock)
        {
            _broadcastWriter.Reset();
            _broadcastWriter.Put((byte)type);
            _broadcastWriter.Put(data, offset, length);

            foreach (var id in peerIds)
            {
                if (_peers.TryGetValue(id, out var peer))
                {
                    peer.Peer.Send(_broadcastWriter, deliveryMethod);
                }
            }
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _netManager.Stop();
    }

    // INetEventListener
    public void OnPeerConnected(NetPeer peer)
    {
        var wrapped = new LiteNetLibPeer(peer);
        _peers.TryAdd(peer.Id, wrapped);
        _connectHandler(wrapped);
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (_peers.TryRemove(peer.Id, out var wrapped))
        {
            _disconnectHandler(wrapped);
            // Cleanup groups? Lazy cleanup or explicit?
            // For now rely on LeaveGroupAsync being called by logic or just lazy
            // In a real app we'd scrub the ID from all groups
        }
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
    {
        if (_peers.TryGetValue(peer.Id, out var wrapped))
        {
            byte[] data = reader.GetRemainingBytes();
            _packetHandler(wrapped, data);
        }
        reader.Recycle();
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) { }
    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    public void OnConnectionRequest(ConnectionRequest request) 
    {
        if (_netManager.ConnectedPeersCount < 100 /* MaxPeers */)
            request.AcceptIfKey("GameKey");
        else
            request.Reject();
    }
}
