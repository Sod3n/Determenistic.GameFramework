using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;
using Guid = System.Guid;

namespace Deterministic.GameFramework.ServerV2;

public class MatchBroadcaster : IDisposable
{
    private readonly MatchManager _matchManager;
    private readonly INetworkServer _networkServer;
    private readonly GamePacketProcessor _packetProcessor;

    // Per-match byte buffer
    private readonly ConcurrentDictionary<Guid, PacketBuffer> _buffers = new();

    public MatchBroadcaster(MatchManager matchManager, INetworkServer networkServer, GamePacketProcessor packetProcessor)
    {
        _matchManager = matchManager;
        _networkServer = networkServer;
        _packetProcessor = packetProcessor;

        _matchManager.OnMatchCreated += OnMatchCreated;
        _matchManager.OnMatchRemoved += OnMatchRemoved;
    }

    private void OnMatchCreated(Match match)
    {
        _buffers[match.Id] = new PacketBuffer();

        // Drain incoming action batches on the game loop thread (before tick logic runs).
        // This eliminates the race between network-thread scheduling and Tick()'s dirty-tick read.
        _packetProcessor.RegisterMatch(match);

        match.Scheduler.OnActionScheduled += (denseId, data, targetId, tick, originalTick, predictionId) =>
            OnActionScheduled(match.Id, denseId, data, targetId, tick, originalTick, predictionId);

        match.Loop.OnTick += () => OnMatchTick(match);
    }

    private void OnMatchRemoved(Match match)
    {
        _buffers.TryRemove(match.Id, out _);
        _packetProcessor.UnregisterMatch(match.Id);
    }

    private void OnActionScheduled(Guid matchId, DenseComponentId componentId, ReadOnlySpan<byte> data, int targetId, long tick, long originalTick, long predictionId)
    {
        var match = _matchManager.GetMatch(matchId);
        if (match != null && match.Loop.Simulation.Strategy.SuppressActionBroadcast) return;

        if (_buffers.TryGetValue(matchId, out var buffer))
        {
            int headerSize = Marshal.SizeOf<NetworkActionHeader>();
            int totalSize = headerSize + data.Length;

            // Lock buffer for thread safety if actions are scheduled from multiple threads
            lock (buffer)
            {
                var span = buffer.GetSpan(totalSize);

                var header = new NetworkActionHeader
                {
                    ComponentId = componentId,
                    TargetEntityId = targetId,
                    ExecuteTick = tick,
                    OriginalExecuteTick = originalTick,
                    PredictionId = predictionId,
                    DataLength = data.Length
                };
                
                MemoryMarshal.Write(span, in header);
                data.CopyTo(span.Slice(headerSize));
                
                buffer.Advance(totalSize);
            }
        }
    }

    private void OnMatchTick(Match match)
    {
        if (match.Loop.Simulation.Strategy.SuppressTickSnapshotBroadcast) return;

        if (_buffers.TryGetValue(match.Id, out var buffer))
        {
            byte[] payload;
            lock (buffer)
            {
                if (buffer.Length == 0) return; // Nothing to send
                payload = buffer.ToArray();
                buffer.Reset();
            }

            // Create binary packet: Header + Payload
            int headerSize = Marshal.SizeOf<TickSnapshotHeader>();
            byte[] packetData = new byte[headerSize + payload.Length];
            
            var header = new TickSnapshotHeader
            {
                ServerTick = match.Loop.CurrentTick,
                PayloadLength = payload.Length,
                MinAllowedTick = match.Scheduler.MinAllowedTick
            };

            var span = new Span<byte>(packetData);
            MemoryMarshal.Write(span, in header);
            payload.CopyTo(span.Slice(headerSize));

            // Broadcast raw bytes
            _ = _networkServer.BroadcastToGroupAsync(match.Id.ToString(), packetData, PacketType.TickSnapshot);
        }
    }

    public void Dispose()
    {
        _matchManager.OnMatchCreated -= OnMatchCreated;
        _matchManager.OnMatchRemoved -= OnMatchRemoved;
    }
}
