using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Network.Server;

public class StateVerificationService : IDisposable
{
    private readonly Match _match;
    private readonly INetworkServer _networkServer;
    private readonly int _intervalTicks;
    private bool _disposed;

    public StateVerificationService(Match match, INetworkServer networkServer, int intervalTicks = 60)
    {
        Console.WriteLine($"[StateVerificationService] Initialized for Match {match.Id}. Interval: {intervalTicks} ticks.");
        _match = match;
        _networkServer = networkServer;
        _intervalTicks = intervalTicks;
        _match.Loop.OnTick += OnTick;
    }

    private void OnTick()
    {
        if (_disposed) return;
        
        long currentTick = _match.Loop.CurrentTick;

        // Verify every N ticks
        if (currentTick % _intervalTicks == 0)
        {
            var hash = StateHasher.Hash(_match.State);
            
            // Note: OnTick runs *after* simulation and *after* CurrentTick is incremented in GameLoop.
            // So _match.State is the result of 'currentTick'.
            // The client stores this state at 'currentTick' in its history.
            
            var packet = new StateHashPacket
            {
                Tick = currentTick,
                Hash = (System.Guid)hash
            };
            
            BroadcastHash(packet);
        }
    }

    private void BroadcastHash(StateHashPacket packet)
    {
        ILogger.Log($"[StateVerification] Broadcasting Hash for Tick {packet.Tick}: {packet.Hash}");
        int size = Marshal.SizeOf<StateHashPacket>();
        byte[] data = new byte[size];
        
        var span = new Span<byte>(data);
        MemoryMarshal.Write(span, ref packet);
        
        // Fire and forget
        _ = _networkServer.BroadcastToGroupAsync(_match.Id.ToString(), data, PacketType.StateHash);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _match.Loop.OnTick -= OnTick;
    }
}
