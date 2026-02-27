using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Packets;
using Deterministic.GameFramework.NetworkV2.Server;

namespace Deterministic.GameFramework.ServerV2;

public class GameHub : Hub
{
    private readonly MatchManager _matchManager;
    private readonly IAuthService _authService;

    public GameHub(MatchManager matchManager, IAuthService authService)
    {
        _matchManager = matchManager;
        _authService = authService;
    }

    public async Task JoinMatch(Guid matchId, string? authToken = null)
    {
        Console.WriteLine($"[GameHub] Entering JoinMatch. matchId={matchId}, connectionId={Context.ConnectionId}");
        try
        {
            Console.WriteLine($"[GameHub] JoinMatch requested for match {matchId}");
            var match = _matchManager.GetMatch(matchId);
            if (match == null)
            {
                Console.WriteLine($"[GameHub] Match {matchId} not found");
                await Clients.Caller.SendAsync("Error", "Match not found");
                return;
            }

            // Authenticate player using injected service
            var playerId = await _authService.AuthenticateAsync(Context.ConnectionId, authToken);
            Console.WriteLine($"[GameHub] Authenticated player {playerId} for connection {Context.ConnectionId}");
            
            match.AddPlayer(playerId);
            Console.WriteLine($"[GameHub] Added player {playerId} to match {matchId}");
            
            await Groups.AddToGroupAsync(Context.ConnectionId, matchId.ToString());
            await Clients.Caller.SendAsync("JoinedMatch", matchId);
            Console.WriteLine($"[GameHub] JoinMatch successful for player {playerId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] FATAL Error in JoinMatch: {ex.Message}");
            Console.WriteLine($"[GameHub] StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public Task SendBatch(Guid matchId, byte[] payload)
    {
        var match = _matchManager.GetMatch(matchId);
        if (match == null) return Task.CompletedTask;

        // Parse batch
        var span = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        
        while (offset + headerSize <= span.Length)
        {
            var header = MemoryMarshal.Read<NetworkActionHeader>(span.Slice(offset));
            offset += headerSize;
            
            if (offset + header.DataLength > span.Length) break;
            
            var dataSpan = span.Slice(offset, header.DataLength);
            offset += header.DataLength;
            
            // Schedule
            match.Scheduler.ScheduleFromBytes(header.NetworkId, dataSpan, header.TargetEntityId, header.ExecuteTick);
        }
        
        return Task.CompletedTask;
    }

    public async Task RequestFullState(Guid matchId)
    {
        Console.WriteLine($"[GameHub] FullState requested for match {matchId} from {Context.ConnectionId}");
        var match = _matchManager.GetMatch(matchId);
        if (match == null) 
        {
            Console.WriteLine($"[GameHub] Match {matchId} not found for FullState request");
            return;
        }

        byte[] stateData;
        long currentTick;

        // Thread safety: Serialize state while pausing the loop or ensuring no write
        lock (match.State) 
        {
            currentTick = match.Loop.CurrentTick;
            stateData = StateSerializer.Serialize(match.State);
        }

        // Create binary packet synchronously to avoid Span in async method
        byte[] packetData = CreateFullStatePacket(currentTick, stateData);

        Console.WriteLine($"[GameHub] Sending FullState (Tick: {currentTick}, Size: {packetData.Length}) to {Context.ConnectionId}");
        await Clients.Caller.SendAsync("OnFullStateReceived", packetData);
    }

    private static byte[] CreateFullStatePacket(long tick, byte[] stateData)
    {
        // Create binary packet: Header + StateData
        int headerSize = Marshal.SizeOf<FullStateHeader>();
        byte[] packetData = new byte[headerSize + stateData.Length];

        var header = new FullStateHeader
        {
            Tick = tick,
            StateDataLength = stateData.Length
        };

        var span = new Span<byte>(packetData);
        MemoryMarshal.Write(span, in header);
        stateData.CopyTo(span.Slice(headerSize));
        
        return packetData;
    }
}
