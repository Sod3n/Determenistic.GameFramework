using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Packets;

namespace Deterministic.GameFramework.NetworkV2.Server;

public class GamePacketProcessor
{
    private readonly MatchManager _matchManager;
    private readonly IServiceProvider _serviceProvider;
    private INetworkServer? _networkServer;

    private INetworkServer NetworkServer => _networkServer ??= _serviceProvider.GetRequiredService<INetworkServer>();

    public GamePacketProcessor(MatchManager matchManager, IServiceProvider serviceProvider)
    {
        _matchManager = matchManager;
        _serviceProvider = serviceProvider;
    }

    public async Task JoinMatchAsync(Guid matchId, string? authToken, INetworkPeer peer, IAuthService authService)
    {
        Console.WriteLine($"[GamePacketProcessor] JoinMatch requested for match {matchId} by {peer.Id}");
        var match = _matchManager.GetMatch(matchId);
        if (match == null)
        {
            Console.WriteLine($"[GamePacketProcessor] Match {matchId} not found");
            // Optionally send error back
            return;
        }

        // Authenticate (Passed in service to decouple or injected)
        // Assuming IAuthService is available here or passed
        var playerId = await authService.AuthenticateAsync(peer.Id, authToken);
        
        match.AddPlayer(playerId);
        
        // Add to Network Group
        await NetworkServer.JoinGroupAsync(peer, matchId.ToString());
        
        // Send MatchJoined confirmation with PlayerId
        var packet = new MatchJoinedPacket { PlayerId = playerId };
        var packetData = new byte[16];
        packet.PlayerId.TryWriteBytes(packetData);
        
        await peer.SendAsync(packetData, PacketType.MatchJoined);
        
        Console.WriteLine($"[GamePacketProcessor] Player {playerId} ({peer.Id}) joined match {matchId}");
    }

    public async Task ProcessBatchAsync(Guid matchId, byte[] payload, INetworkPeer peer)
    {
        var match = _matchManager.GetMatch(matchId);
        if (match == null) return;

        bool fullStateRequired = ProcessBatchInternal(match, payload);

        if (fullStateRequired)
        {
            await RequestFullStateAsync(matchId, peer);
        }
    }

    private bool ProcessBatchInternal(Match match, byte[] payload)
    {
        var span = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        bool fullStateRequired = false;

        while (offset + headerSize <= span.Length)
        {
            var header = MemoryMarshal.Read<NetworkActionHeader>(span.Slice(offset));
            offset += headerSize;
            
            if (offset + header.DataLength > span.Length) break;
            
            var dataSpan = span.Slice(offset, header.DataLength);
            offset += header.DataLength;
            
            // Schedule
            var result = match.Scheduler.ScheduleFromBytes(header.NetworkId, dataSpan, header.TargetEntityId, header.ExecuteTick);

            if (result == ActionScheduler.ScheduleResult.TooOld)
            {
                 fullStateRequired = true;
            }
        }
        return fullStateRequired;
    }

    public async Task RequestFullStateAsync(Guid matchId, INetworkPeer peer)
    {
        var match = _matchManager.GetMatch(matchId);
        if (match == null) return;

        byte[] packetData = CreateFullStatePacket(match);
        
        await peer.SendAsync(packetData, PacketType.FullState);
    }

    private byte[] CreateFullStatePacket(Match match)
    {
        byte[] stateData;
        long currentTick;

        lock (match.State) 
        {
            currentTick = match.Loop.CurrentTick;
            stateData = StateSerializer.Serialize(match.State);
        }

        // Create Packet
        int headerSize = Marshal.SizeOf<FullStateHeader>();
        byte[] packetData = new byte[headerSize + stateData.Length];

        var header = new FullStateHeader
        {
            Tick = currentTick,
            StateDataLength = stateData.Length
        };

        var span = new Span<byte>(packetData);
        MemoryMarshal.Write(span, in header);
        stateData.CopyTo(span.Slice(headerSize));
        
        return packetData;
    }
}
