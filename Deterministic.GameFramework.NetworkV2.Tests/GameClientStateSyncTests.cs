using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.NetworkV2.Client;
using Deterministic.GameFramework.NetworkV2.Interfaces;
using Deterministic.GameFramework.NetworkV2.Packets;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.NetworkV2.Tests;

[NetworkId("00000000-0000-0000-0000-000000000001")]
public struct TestComponent : IComponent
{
    public int Value;
}

public class GameClientStateSyncTests
{
    private class MockNetworkClient : INetworkClient
    {
        public event Action<byte[]>? OnTickSnapshotReceived;
        public event Action<byte[]>? OnFullStateReceived;
        public event Action<byte[]>? OnComponentMappingReceived;
        public event Action<byte[]>? OnStateHashReceived;
        public event Action? OnConnected;
        public event Action? OnDisconnected;

        public int Ping => 0;

        public List<System.Guid> RequestedFullStates = new();
        public List<byte[]> SentBatches = new();

        public Task ConnectAsync(string address) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<System.Guid> JoinMatchAsync(System.Guid matchId, string? token = null) => Task.FromResult(System.Guid.NewGuid());

        public Task RequestFullStateAsync(System.Guid matchId)
        {
            RequestedFullStates.Add(matchId);
            return Task.CompletedTask;
        }

        public void SendBatch(byte[] data)
        {
            SentBatches.Add(data);
        }

        // Helper to simulate incoming packets
        public void SimulateStateHash(long tick, System.Guid hash)
        {
            var packet = new StateHashPacket
            {
                Tick = tick,
                Hash = hash
            };
            
            int size = Marshal.SizeOf<StateHashPacket>();
            byte[] data = new byte[size];
            MemoryMarshal.Write(new Span<byte>(data), in packet);
            
            OnStateHashReceived?.Invoke(data);
        }
    }

    [NetworkId("00000000-0000-0000-0000-000000000001")]
    public struct TestComponent : IComponent
    {
        public int Value;
    }

    [Fact]
    public void StateHasher_ShouldBe_Deterministic()
    {
        // Arrange
        var state1 = new GlobalState();
        var state2 = new GlobalState();

        // Setup identical states
        SetupState(state1);
        SetupState(state2);

        // Act
        var hash1 = StateHasher.Hash(state1);
        var hash2 = StateHasher.Hash(state2);

        // Assert - Same state = Same hash
        hash1.Should().Be(hash2);

        // Act - Modify one state
        var entity = state2.CreateEntity(); 
        state2.AddComponent(entity, new TestComponent { Value = 999 });
        
        var hash3 = StateHasher.Hash(state2);

        // Assert - Different state = Different hash
        hash3.Should().NotBe(hash1);
    }

    private void SetupState(GlobalState state)
    {
        var e1 = state.CreateEntity();
        state.AddComponent(e1, new TestComponent { Value = 123 });
    }

    [Fact]
    public void GameClient_Should_Detect_StateMismatch_And_RequestFullState()
    {
        // Arrange
        var mockNet = new MockNetworkClient();
        var game = new Game(tickRate: 60);
        var client = new GameClient(mockNet, "localhost", game);

        // Simulate client running to Tick 10
        for (int i = 0; i < 10; i++)
        {
            game.Loop.RunSingleTick(); 
        }
        // CurrentTick is now 10 (or 11 depending on RunSingleTick impl). 
        // RunSingleTick calls Tick(), which increments at the end. 
        // So 10 calls -> Tick 0...9 processed -> CurrentTick becomes 10.
        // History should contain states for 1..10.
        
        long targetTick = 5;
        // Get the actual local hash for Tick 5
        game.Loop.History.TryGetSnapshotData(targetTick, out byte[]? realData).Should().BeTrue();
        var localHash = StateHasher.Hash(realData!);
        
        // Create a DIFFERENT hash for the same tick
        var serverHash = System.Guid.NewGuid();

        bool mismatchDetected = false;
        client.OnStateMismatch += (tick, local, server) => 
        {
            mismatchDetected = true;
            tick.Should().Be(targetTick);
            local.Should().Be((System.Guid)localHash); // Cast wrapper GUID to System.Guid
            server.Should().Be(serverHash);
        };

        // Act
        mockNet.SimulateStateHash(targetTick, serverHash);

        // Assert
        mismatchDetected.Should().BeTrue("Client should raise OnStateMismatch event");
        mockNet.RequestedFullStates.Should().HaveCount(1, "Client should request full state sync");
    }

    [Fact]
    public void GameClient_Should_Verify_Correct_State()
    {
        // Arrange
        var mockNet = new MockNetworkClient();
        var game = new Game(tickRate: 60);
        var client = new GameClient(mockNet, "localhost", game);

        // Simulate client running
        for (int i = 0; i < 10; i++)
        {
            game.Loop.RunSingleTick(); 
        }
        
        long targetTick = 5;
        game.Loop.History.TryGetSnapshotData(targetTick, out byte[]? realData).Should().BeTrue();
        // Calculate expected hash (need to convert from framework Guid to System.Guid if needed, but StateHasher returns framework Guid?)
        // StateHasher.Hash returns Deterministic.GameFramework.CoreV2.Guid
        var localHash = StateHasher.Hash(realData!);
        
        // Simulate receiving the SAME hash
        // Note: Packet uses System.Guid? 
        // Let's check StateHashPacket definition.
        
        // Act
        mockNet.SimulateStateHash(targetTick, (System.Guid)localHash); // Implicit/Explicit cast

        // Assert
        mockNet.RequestedFullStates.Should().BeEmpty();
    }
}
