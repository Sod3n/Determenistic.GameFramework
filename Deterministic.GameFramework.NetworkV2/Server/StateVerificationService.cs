using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Packets;
using Deterministic.GameFramework.NetworkV2.Server;

namespace Deterministic.GameFramework.NetworkV2.Server;

public class StateVerificationService : IDisposable
{
    private readonly Match _match;
    private readonly INetworkServer _networkServer;
    private readonly int _intervalTicks;
    private bool _disposed;

    public StateVerificationService(Match match, INetworkServer networkServer, int intervalTicks = 60)
    {
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
            
            // Note: OnTick runs *after* simulation but *before* CurrentTick is incremented in GameLoop.
            // So _match.State is the result of 'currentTick'.
            // The client stores this state at 'currentTick + 1' in its history.
            // To align, we send the tick as 'currentTick + 1'.
            
            var packet = new StateHashPacket
            {
                Tick = currentTick + 1,
                Hash = (System.Guid)hash
            };
            
            BroadcastHash(packet);
        }
    }

    private void BroadcastHash(StateHashPacket packet)
    {
        int size = Marshal.SizeOf<StateHashPacket>();
        byte[] data = new byte[size];
        
        var span = new Span<byte>(data);
        MemoryMarshal.Write(span, in packet);
        
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
