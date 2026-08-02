using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Network.Server;

public class GamePacketProcessor
{
    private MatchManager? _matchManager;
    private readonly IServiceProvider _serviceProvider;
    private INetworkServer? _networkServer;

    private INetworkServer NetworkServer => _networkServer ??= _serviceProvider.GetRequiredService<INetworkServer>();
    private MatchManager MatchManager => _matchManager ??= _serviceProvider.GetRequiredService<MatchManager>();

    // Per-match queue: network thread enqueues, game loop thread drains via OnBeforeTick.
    // Eliminates the race between ScheduleFromBytes and Tick()'s EarliestDirtyTick read.
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<(byte[] payload, INetworkPeer peer)>> _incomingBatches = new();

    public GamePacketProcessor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Register a match so incoming actions are queued and drained on the game loop thread.
    /// Call this when the match is created (e.g. from MatchBroadcaster.OnMatchCreated).
    /// </summary>
    public void RegisterMatch(Match match)
    {
        _incomingBatches[match.Id] = new ConcurrentQueue<(byte[], INetworkPeer)>();
        match.Loop.OnBeforeTick += () => DrainMatchQueue(match);
    }

    /// <summary>
    /// Unregister a match and remove its queue.
    /// </summary>
    public void UnregisterMatch(Guid matchId)
    {
        _incomingBatches.TryRemove(matchId, out _);
    }

    public async Task JoinMatchAsync(System.Guid matchId, string? authToken, INetworkPeer peer, IAuthService authService)
    {
        ILogger.Log($"[GamePacketProcessor] JoinMatch requested for match {matchId} by {peer.Id}");
        var match = MatchManager.GetMatch(matchId);
        if (match == null)
        {
            ILogger.LogError($"[GamePacketProcessor] Match {matchId} not found");
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
        packet.PlayerId.ToByteArray().CopyTo(packetData, 0);

        await peer.SendAsync(packetData, PacketType.MatchJoined);

        ILogger.Log($"[GamePacketProcessor] Player {playerId} ({peer.Id}) joined match {matchId}");
    }

    public Task ProcessBatchAsync(System.Guid matchId, byte[] payload, INetworkPeer peer)
    {
        // Enqueue for processing on the game loop thread.
        if (_incomingBatches.TryGetValue(matchId, out var queue))
        {
            queue.Enqueue((payload, peer));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drain all queued action batches for a match. Called on the game loop thread via OnBeforeTick.
    /// </summary>
    private void DrainMatchQueue(Match match)
    {
        if (!_incomingBatches.TryGetValue(match.Id, out var queue)) return;

        while (queue.TryDequeue(out var item))
        {
            bool fullStateRequired = ProcessBatchInternal(match, item.payload);

            if (fullStateRequired)
            {
                _ = RequestFullStateAsync(match.Id, item.peer);
            }
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

            // Schedule — forward the client's PredictionId so the server-side execute
            // path tags the created entity with PredictedByAction(PID). That tag rides
            // back in the delta so the originating client can correlate.
            if (ComponentId.TryFromDense(header.ComponentId, out var networkId))
            {
                var result = match.Scheduler.ScheduleFromBytes(networkId.ToDense(), dataSpan, header.TargetEntityId, header.ExecuteTick, header.PredictionId);

                // Only log non-Duplicate actions to avoid flooding stdout with GGPO resends
                // (thousands of lines/second with 2+ clients → 100% CPU from console I/O alone)
                if (result != ActionScheduler.ScheduleResult.Duplicate)
                {
                    string actionName = ComponentId.TryGetType(networkId.ToDense(), out var actionType) && actionType != null
                        ? actionType.Name : $"CID({header.ComponentId})";
                    ILogger.Log($"[Server] Action: {actionName} target={header.TargetEntityId} " +
                        $"execTick={header.ExecuteTick} result={result} (serverTick={match.Loop.CurrentTick})");
                }

                // TooOld is expected with redundant sending (GGPO): the client resends all
                // pending actions every tick, so stale copies arrive after MinAllowedTick
                // has advanced. This is NOT a desync — the original was already processed.
                // Real desyncs are caught by StateVerificationService (hash comparison).
            }
            else
            {
                ILogger.LogWarning($"[GamePacketProcessor] Warning: Received unknown ComponentId {header.ComponentId}. Skipping.");
            }
        }
        return fullStateRequired;
    }

    public async Task RequestFullStateAsync(System.Guid matchId, INetworkPeer peer)
    {
        var match = MatchManager.GetMatch(matchId);
        if (match == null) return;

        byte[] packetData = CreateFullStatePacket(match);

        await peer.SendAsync(packetData, PacketType.FullState);
    }

    private byte[] CreateFullStatePacket(Match match)
    {
        byte[] stateData;
        long stateTick;
        long serverTick;

        lock (match.State)
        {
            if (match.FullStateProvider != null)
            {
                stateData = match.FullStateProvider.GetFullState(match, out stateTick, out serverTick);
            }
            else
            {
                serverTick = match.Loop.CurrentTick;
                stateData = StateSerializer.Serialize(match.State);
                stateTick = serverTick;
            }
        }

        int headerSize = Marshal.SizeOf<FullStateHeader>();
        byte[] packetData = new byte[headerSize + stateData.Length];

        var header = new FullStateHeader
        {
            Tick = stateTick,
            ServerTick = serverTick,
            StateDataLength = stateData.Length
        };

        var span = new Span<byte>(packetData);
        MemoryMarshal.Write(span, ref header);
        stateData.CopyTo(span.Slice(headerSize));

        return packetData;
    }
}
