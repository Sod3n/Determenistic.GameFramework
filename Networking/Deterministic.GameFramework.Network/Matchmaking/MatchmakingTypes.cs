using System;
using Deterministic.GameFramework.Network.Interfaces;

namespace Deterministic.GameFramework.Network.Server;

public struct MatchmakingPlayer
{
    public Guid PlayerId;
    public INetworkPeer Peer;
}

public class Lobby
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public System.Collections.Generic.List<MatchmakingPlayer> Players { get; } = new();
}
