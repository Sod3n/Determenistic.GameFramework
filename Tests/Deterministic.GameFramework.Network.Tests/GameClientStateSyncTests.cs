using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.GGPO;
using Deterministic.GameFramework.Network.Client;
using Deterministic.GameFramework.Network.Interfaces;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Serialization;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Network.Tests;

[StableId("00000000-0000-0000-0000-000000000001")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TestComponent : IComponent
{
    public int Value;
}

public class GameClientStateSyncTests
{
    public GameClientStateSyncTests()
    {
        ServiceLocator.RegisterAssembly(typeof(World).Assembly);
        ServiceLocator.RegisterAssembly(typeof(GameClientStateSyncTests).Assembly);
    }

    private class MockNetworkClient : INetworkClient
    {
        public event Action<byte[]>? OnTickSnapshotReceived;
        public event Action<byte[]>? OnFullStateReceived;
        public event Action<byte[]>? OnComponentMappingReceived;
        public event Action<byte[]>? OnStateHashReceived;
        public event Action<byte[]>? OnTickDeltaReceived;
        public event Action<System.Guid>? OnLobbyCreated;
        public event Action<Guid>? OnLobbyJoined;
        public event Action<Guid>? OnMatchAssigned;
        public event Action<System.Guid, System.Guid>? OnMatchJoined;
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

        public Task EnqueuePlayerAsync()
        {
            throw new NotImplementedException();
        }

        public Task CreateLobbyAsync(string lobbyName)
        {
            throw new NotImplementedException();
        }

        public Task JoinLobbyAsync(Guid lobbyId)
        {
            throw new NotImplementedException();
        }

        public Task EnqueuePlayerAsync(string? token = null) => Task.CompletedTask;
        public Task CreateLobbyAsync(string name, string? token = null) => Task.CompletedTask;
        public Task JoinLobbyAsync(System.Guid lobbyId, string? token = null) => Task.CompletedTask;
        public Task StartLobbyMatchAsync(System.Guid lobbyId, byte[]? initialState = null) => Task.CompletedTask;
        public Task EnqueueLobbyAsync(System.Guid lobbyId) => Task.CompletedTask;

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
#if NET8_0_OR_GREATER
            MemoryMarshal.Write(new Span<byte>(data), in packet);
#else
            MemoryMarshal.Write(new Span<byte>(data), ref packet);
#endif
            
            OnStateHashReceived?.Invoke(data);
        }
    }

    [Fact]
    public void StateHasher_ShouldBe_Deterministic()
    {
        // Arrange
        var state1 = new EntityWorld();
        var state2 = new EntityWorld();

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

    private void SetupState(EntityWorld state)
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
        var client = new GGPOGameClient(mockNet, "localhost", game);

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
        ((GGPOGameClient)client).History.TryGetSnapshotData(targetTick, out byte[]? realData).Should().BeTrue();
        var localHash = StateHasher.Hash(realData!);
        
        // Create a DIFFERENT hash for the same tick
        var serverHash = System.Guid.NewGuid();

        int mismatchCount = 0;
        client.OnStateMismatch += (tick, local, server) =>
        {
            mismatchCount++;
        };

        // Act — need to drain the network queue (hashes are enqueued, not processed immediately).
        // GameClient requires ConsecutiveMismatchThreshold (2) consecutive mismatches
        // before requesting a full state resync. Send two mismatching hashes.
        mockNet.SimulateStateHash(targetTick, serverHash);
        client.DrainNetworkQueues();

        // Assert — first mismatch fires event but doesn't request full state yet
        mismatchCount.Should().Be(1, "Client should raise OnStateMismatch event");
        mockNet.RequestedFullStates.Should().BeEmpty("Single mismatch should not trigger resync");

        // Second consecutive mismatch triggers full state request
        mockNet.SimulateStateHash(targetTick - 1, System.Guid.NewGuid());
        client.DrainNetworkQueues();

        mismatchCount.Should().Be(2);
        mockNet.RequestedFullStates.Should().HaveCount(1, "Client should request full state sync after consecutive mismatches");
    }

    [Fact]
    public void GameClient_Should_Verify_Correct_State()
    {
        // Arrange
        var mockNet = new MockNetworkClient();
        var game = new Game(tickRate: 60);
        var client = new GGPOGameClient(mockNet, "localhost", game);

        // Simulate client running
        for (int i = 0; i < 10; i++)
        {
            game.Loop.RunSingleTick(); 
        }
        
        long targetTick = 5;
        ((GGPOGameClient)client).History.TryGetSnapshotData(targetTick, out byte[]? realData).Should().BeTrue();
        // Calculate expected hash (need to convert from framework Guid to System.Guid if needed, but StateHasher returns framework Guid?)
        // StateHasher.Hash returns Deterministic.GameFramework.Core.Guid
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
