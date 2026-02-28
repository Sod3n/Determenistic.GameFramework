using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using Deterministic.GameFramework.NetworkV2.Packets;

namespace Deterministic.GameFramework.NetworkV2.Server;

public class LiteNetLibPacketRouter
{
    private readonly GamePacketProcessor _processor;
    private readonly IAuthService _authService;
    private readonly ConcurrentDictionary<string, Guid> _peerSessions = new();

    public LiteNetLibPacketRouter(GamePacketProcessor processor, IAuthService authService)
    {
        _processor = processor;
        _authService = authService;
    }

    public Task OnPeerConnected(INetworkPeer peer)
    {
        return Task.CompletedTask;
    }

    public Task OnPeerDisconnected(INetworkPeer peer)
    {
        _peerSessions.TryRemove(peer.Id, out _);
        return Task.CompletedTask;
    }

    public async Task OnPacketReceived(INetworkPeer peer, byte[] data)
    {
        if (data.Length == 0) return;
        
        // Use NetDataReader to parse safely
        var reader = new NetDataReader(data);
        if (!reader.TryGetByte(out byte typeByte)) return;
        
        PacketType type = (PacketType)typeByte;

        switch (type)
        {
            case PacketType.JoinMatch:
            {
                // Format: [Type][MatchId(16)][Token(String)]
                if (reader.AvailableBytes < 16) return;
                
                byte[] matchIdBytes = new byte[16];
                reader.GetBytes(matchIdBytes, 16);
                
                var matchId = new Guid(matchIdBytes);
                var token = reader.GetString(); // Handles null/empty safely usually
                
                // Track session
                _peerSessions[peer.Id] = matchId;
                
                await _processor.JoinMatchAsync(matchId, token, peer, _authService);
                break;
            }
            
            case PacketType.Batch:
            {
                if (_peerSessions.TryGetValue(peer.Id, out var matchId))
                {
                    // Payload is everything after the Type byte
                    // We can slice the original array or copy
                    var payload = new byte[data.Length - 1];
                    Array.Copy(data, 1, payload, 0, payload.Length);
                    
                    await _processor.ProcessBatchAsync(matchId, payload, peer);
                }
                break;
            }
            
            case PacketType.RequestFullState:
            {
                if (_peerSessions.TryGetValue(peer.Id, out var existingMatchId))
                {
                    await _processor.RequestFullStateAsync(existingMatchId, peer);
                }
                else
                {
                    // Maybe the RequestFullState packet contains the matchId?
                    // Let's check LiteNetLibNetworkClient.RequestFullStateAsync
                    // It sends [Type][MatchId]
                    
                    if (reader.AvailableBytes >= 16)
                    {
                        byte[] matchIdBytes = new byte[16];
                        reader.GetBytes(matchIdBytes, 16);
                        
                        var requestedMatchId = new Guid(matchIdBytes);
                        _peerSessions[peer.Id] = requestedMatchId; // Implicit join/restore?
                        await _processor.RequestFullStateAsync(requestedMatchId, peer);
                    }
                }
                break;
            }
        }
    }
}
