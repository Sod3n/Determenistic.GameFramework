using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.GGPO;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Network.Tests;

public class StateVerificationServiceTests
{
    public StateVerificationServiceTests()
    {
        ServiceLocator.RegisterAssembly(typeof(World).Assembly);
    }

    private class MockNetworkServer : INetworkServer
    {
        public List<(string group, byte[] data, PacketType type)> Broadcasts = new();

        public Task BroadcastToGroupAsync(string groupName, byte[] data, PacketType type)
        {
            Broadcasts.Add((groupName, data, type));
            return Task.CompletedTask;
        }

        public Task JoinGroupAsync(INetworkPeer peer, string groupName) => Task.CompletedTask;
        public Task LeaveGroupAsync(INetworkPeer peer, string groupName) => Task.CompletedTask;
    }

    [Fact]
    public void Should_Broadcast_On_Interval_Correctly()
    {
        var state = new EntityWorld();
        var game = new Game(state, tickRate: 60);

        var matchId = System.Guid.NewGuid();
        var match = new Match(matchId, game);

        var history = GGPOSetup.EnableGGPO(game.Loop.Simulation);
        var strategy = (GGPOSyncStrategy)game.Loop.Simulation.Strategy;

        var mockServer = new MockNetworkServer();
        var interval = 10;
        var service = new StateVerificationService(match, mockServer, history, strategy, interval);
        service.ConfirmationWindowTicks = 0;

        for (int i = 0; i < 10; i++)
        {
            game.Loop.RunSingleTick();
        }
        mockServer.Broadcasts.Should().HaveCount(1);
        mockServer.Broadcasts[0].data.Should().NotBeEmpty();

        for (int i = 0; i < 9; i++)
        {
            game.Loop.RunSingleTick();
        }
        mockServer.Broadcasts.Should().HaveCount(1);

        game.Loop.RunSingleTick();
        mockServer.Broadcasts.Should().HaveCount(2);
    }

    [Fact]
    public void Service_Should_Broadcast_Correct_Packet()
    {
        var game = new Game(tickRate: 60);
        var matchId = System.Guid.NewGuid();
        var match = new Match(matchId, game);

        var history = GGPOSetup.EnableGGPO(game.Loop.Simulation);
        var strategy = (GGPOSyncStrategy)game.Loop.Simulation.Strategy;

        var mockServer = new MockNetworkServer();
        var service = new StateVerificationService(match, mockServer, history, strategy, intervalTicks: 1);
        service.ConfirmationWindowTicks = 0;

        game.Loop.RunSingleTick();

        mockServer.Broadcasts.Should().HaveCount(1);
        var broadcast = mockServer.Broadcasts[0];

        broadcast.group.Should().Be(matchId.ToString());
        broadcast.type.Should().Be(PacketType.StateHash);

        var span = new ReadOnlySpan<byte>(broadcast.data);
        var packet = MemoryMarshal.Read<StateHashPacket>(span);

        packet.Tick.Should().Be(1);
        packet.Hash.Should().NotBeEmpty();
    }
}
