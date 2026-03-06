using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Packets;
using Deterministic.GameFramework.NetworkV2.Server;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.NetworkV2.Tests;

public class StateVerificationServiceTests
{
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
         // Arrange
        var state = new GlobalState();
        // Use Game constructor to ensure dependencies are linked
        var game = new Game(state, tickRate: 60);
        
        var matchId = System.Guid.NewGuid();
        var match = new Match(matchId, game);
        
        var mockServer = new MockNetworkServer();
        var interval = 10;
        var service = new StateVerificationService(match, mockServer, interval);

        // Act & Assert
        
        // Tick 0: Should broadcast (0 % 10 == 0)
        game.Loop.RunSingleTick(); 
        mockServer.Broadcasts.Should().HaveCount(1);
        mockServer.Broadcasts[0].data.Should().NotBeEmpty();
        
        // Ticks 1-9: Should NOT broadcast
        for (int i = 0; i < 9; i++)
        {
            game.Loop.RunSingleTick();
        }
        mockServer.Broadcasts.Should().HaveCount(1); // Still 1
        
        // Tick 10: Should broadcast
        game.Loop.RunSingleTick();
        mockServer.Broadcasts.Should().HaveCount(2);
    }
    
    [Fact]
    public void Service_Should_Broadcast_Correct_Packet()
    {
        // Arrange
        var game = new Game(tickRate: 60);
        var matchId = System.Guid.NewGuid();
        var match = new Match(matchId, game);
        var mockServer = new MockNetworkServer();
        var service = new StateVerificationService(match, mockServer, intervalTicks: 1); // Every tick

        // Act
        game.Loop.RunSingleTick();

        // Assert
        mockServer.Broadcasts.Should().HaveCount(1);
        var broadcast = mockServer.Broadcasts[0];
        
        broadcast.group.Should().Be(matchId.ToString());
        broadcast.type.Should().Be(PacketType.StateHash);
        
        // Verify Payload
        var span = new ReadOnlySpan<byte>(broadcast.data);
        var packet = MemoryMarshal.Read<StateHashPacket>(span);
        
        // Service logic: Tick = currentTick + 1
        // OnTick runs when CurrentTick=0. So sent Tick should be 1.
        packet.Tick.Should().Be(1);
        packet.Hash.Should().NotBeEmpty();
    }
}
