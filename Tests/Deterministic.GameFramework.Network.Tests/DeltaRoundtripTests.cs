using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.DeltaSync;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Serialization;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Network.Tests;

// Dedicated component types for delta round-trip tests. Kept Pack=1 + unmanaged to match
// the production contract (AlignedComponentStore.CopyElementPacked relies on this).

[StableId("00000000-0000-0000-0000-0000000d0001")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeltaPosition : IComponent
{
    public int X;
    public int Y;
    public int Z;
}

[StableId("00000000-0000-0000-0000-0000000d0002")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeltaVelocity : IComponent
{
    public int Dx;
    public int Dy;
}

[StableId("00000000-0000-0000-0000-0000000d0003")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeltaHealth : IComponent
{
    public int Current;
    public int Max;
}

[StableId("00000000-0000-0000-0000-0000000d0004")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeltaTag : IComponent
{
    public byte Flag;
}

[StableId("00000000-0000-0000-0000-0000000d0005")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DeltaLarge : IComponent
{
    // 64 ints = 256 bytes; intentionally at the StackBufferThreshold boundary used
    // by DeltaGenerator. Exercise the pooled-buffer branch on larger sizes too.
    public long A0, A1, A2, A3, A4, A5, A6, A7, A8, A9;
    public long A10, A11, A12, A13, A14, A15, A16, A17, A18, A19;
    public long A20, A21, A22, A23, A24, A25, A26, A27, A28, A29;
    public long A30, A31, A32, A33;
}

/// <summary>
/// Round-trip tests for DeltaGenerator + DeltaApplier. The contract under test is:
/// applying a delta (server-side) from baseline → current to a receiver world that matches
/// the baseline should leave the receiver bit-equal to the server's current state
/// (as measured by <see cref="StateSerializer.Serialize"/>, which is the authoritative
/// wire representation and the same function used for state hashing).
/// </summary>
public class DeltaRoundtripTests : IDisposable
{
    public DeltaRoundtripTests()
    {
        // The state serializer and the ComponentId registry are global singletons. Clean
        // state ensures tests don't pick up stale mappings from earlier suites running in
        // the same AppDomain.
        ComponentId.UnregisterAll();
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(DeltaRoundtripTests).Assembly);
    }

    public void Dispose()
    {
        ComponentId.ClearMappings();
    }

    [Fact]
    public void Roundtrip_EmptyToEmpty_ProducesNoOps()
    {
        var current = new EntityWorld();
        var baseline = new EntityWorld();
        var gen = new DeltaGenerator();

        // Both worlds start with just the World entity at id 0 (same component bytes).
        var ops = gen.GenerateOpStream(current, baseline);

        // Both worlds have only the World entity with matching bytes — no diff.
        ops.Length.Should().Be(0);
    }

    [Fact]
    public void Roundtrip_SingleEntityCreate_AppliedMatchesSerialized()
    {
        var world = new EntityWorld();
        var baseline = new EntityWorld();

        // Seed baseline to match world (both just have the World entity).
        var applied = new EntityWorld();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 10, Y = 20, Z = 30 });
        world.AddComponent(e1, new DeltaHealth { Current = 80, Max = 100 });

        var gen = new DeltaGenerator();
        var opStream = gen.GenerateOpStream(world, baseline).ToArray();
        DeltaApplier.ApplyOps(applied, opStream);

        AssertWorldsEqual(world, applied);
    }

    [Fact]
    public void Roundtrip_ComponentAdd_AppliedMatches()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });
        SyncToApplied(world, applied);

        // Now add a second component and generate delta vs. the snapshot baseline.
        var baseline = CloneWorld(world);
        world.AddComponent(e1, new DeltaVelocity { Dx = 5, Dy = 6 });

        RoundTripAndAssertEqual(world, baseline, applied);
    }

    [Fact]
    public void Roundtrip_ComponentRemove_AppliedMatches()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });
        world.AddComponent(e1, new DeltaVelocity { Dx = 5, Dy = 6 });
        SyncToApplied(world, applied);

        var baseline = CloneWorld(world);
        world.RemoveComponent<DeltaVelocity>(e1);

        RoundTripAndAssertEqual(world, baseline, applied);
    }

    [Fact]
    public void Roundtrip_ComponentModify_AppliedMatches()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });
        SyncToApplied(world, applied);

        var baseline = CloneWorld(world);
        ref var pos = ref world.GetComponent<DeltaPosition>(e1);
        pos.X = 100;
        pos.Y = 200;

        RoundTripAndAssertEqual(world, baseline, applied);
    }

    [Fact]
    public void Roundtrip_EntityDestroy_AppliedMatches()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });
        var e2 = world.CreateEntity();
        world.AddComponent(e2, new DeltaPosition { X = 7, Y = 8, Z = 9 });
        SyncToApplied(world, applied);

        var baseline = CloneWorld(world);
        world.DeleteEntity(e1);

        RoundTripAndAssertEqual(world, baseline, applied);
    }

    [Fact]
    public void Roundtrip_MultipleMutations_AppliedMatches()
    {
        var (world, applied) = CreateMatchingWorlds();

        // Step 1: initial population
        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 10, Y = 20, Z = 30 });
        world.AddComponent(e1, new DeltaHealth { Current = 50, Max = 100 });

        var e2 = world.CreateEntity();
        world.AddComponent(e2, new DeltaVelocity { Dx = 1, Dy = 2 });

        var baseline = new EntityWorld();
        RoundTripAndAssertEqual(world, baseline, applied);

        // Step 2: mutate a bunch at once
        baseline = CloneWorld(world);
        world.AddComponent(e1, new DeltaVelocity { Dx = -3, Dy = -4 });
        world.RemoveComponent<DeltaHealth>(e1);
        world.GetComponent<DeltaPosition>(e1) = new DeltaPosition { X = 99, Y = 88, Z = 77 };

        var e3 = world.CreateEntity();
        world.AddComponent(e3, new DeltaTag { Flag = 42 });

        world.DeleteEntity(e2);

        RoundTripAndAssertEqual(world, baseline, applied);

        // Step 3: stability check — no changes means no ops and no divergence
        baseline = CloneWorld(world);
        RoundTripAndAssertEqual(world, baseline, applied);
    }

    [Fact]
    public void Roundtrip_LargeComponent_UsesPooledBufferPath()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        var large = new DeltaLarge();
        // Set a recognizable pattern across the first few slots.
        large.A0 = 1; large.A1 = 2; large.A32 = 0x1234567890ABCDEFL; large.A33 = -1;
        world.AddComponent(e1, large);

        RoundTripAndAssertEqual(world, new EntityWorld(), applied);
    }

    [Fact]
    public void PacketFormat_HeaderContainsTickMetadata()
    {
        var world = new EntityWorld();
        var baseline = new EntityWorld();
        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });

        var gen = new DeltaGenerator();
        var packet = gen.Generate(world, baseline, serverTick: 100, baselineTick: 50);

        int headerSize = Marshal.SizeOf<TickDeltaHeader>();
        var header = MemoryMarshal.Read<TickDeltaHeader>(packet);
        header.ServerTick.Should().Be(100);
        header.BaselineTick.Should().Be(50);
        header.PayloadLength.Should().Be(packet.Length - headerSize);
    }

    [Fact]
    public void ApplyPacket_FullRoundtripViaWire()
    {
        var (world, applied) = CreateMatchingWorlds();

        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 7, Y = 8, Z = 9 });
        world.AddComponent(e1, new DeltaHealth { Current = 42, Max = 100 });

        var gen = new DeltaGenerator();
        var packet = gen.Generate(world, new EntityWorld(), serverTick: 10, baselineTick: -1);

        var header = DeltaApplier.ApplyPacket(applied, packet);
        header.ServerTick.Should().Be(10);
        header.BaselineTick.Should().Be(-1);

        AssertWorldsEqual(world, applied);
    }

    [Fact]
    public void Roundtrip_SimulatedServerLoop_BaselineAdvancesCorrectly()
    {
        // Simulates the DeltaSyncStrategyServer loop: baseline advances to the just-sent
        // state after each tick, so successive deltas are incremental.
        var world = new EntityWorld();
        var applied = new EntityWorld();
        var baseline = new EntityWorld();
        var gen = new DeltaGenerator();

        // Tick 1: create e1
        var e1 = world.CreateEntity();
        world.AddComponent(e1, new DeltaPosition { X = 1, Y = 2, Z = 3 });
        ApplyAndAdvance(gen, world, baseline, applied);
        AssertWorldsEqual(world, applied);

        // Tick 2: modify e1, create e2
        ref var pos = ref world.GetComponent<DeltaPosition>(e1);
        pos.X = 100;
        var e2 = world.CreateEntity();
        world.AddComponent(e2, new DeltaVelocity { Dx = 9, Dy = -9 });
        ApplyAndAdvance(gen, world, baseline, applied);
        AssertWorldsEqual(world, applied);

        // Tick 3: remove component, destroy entity
        world.RemoveComponent<DeltaPosition>(e1);
        world.DeleteEntity(e2);
        ApplyAndAdvance(gen, world, baseline, applied);
        AssertWorldsEqual(world, applied);

        // Tick 4: idle — should produce no ops
        var opsBefore = gen.GenerateOpStream(world, baseline);
        opsBefore.Length.Should().Be(0);
    }

    /// <summary>
    /// Regression test for the bit-scan iteration optimization in
    /// <c>DeltaGenerator.EmitComponentDiffs</c>. With many unchanged entities and a single
    /// mutated one, the output op stream must contain exactly one ComponentModify op —
    /// proving the skip/iteration short-circuit neither drops a real change nor emits
    /// spurious ops for entities whose bytes match the baseline.
    /// </summary>
    [Fact]
    public void DeltaGen_SingleMutationAmongManyUnchanged_EmitsExactlyOneModify()
    {
        var world = new EntityWorld();

        // Populate a fleet of 16 entities, each with two components.
        var entities = new Entity[16];
        for (int i = 0; i < entities.Length; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent(entities[i], new DeltaPosition { X = i, Y = i * 2, Z = i * 3 });
            world.AddComponent(entities[i], new DeltaHealth { Current = 100, Max = 100 });
        }

        // Baseline = byte-equivalent snapshot of world.
        var baseline = CloneWorld(world);

        // Mutate exactly one component on one entity.
        ref var pos = ref world.GetComponent<DeltaPosition>(entities[7]);
        pos.X = 9999;

        var gen = new DeltaGenerator();
        var stream = gen.GenerateOpStream(world, baseline).ToArray();

        int modifyCount = 0;
        int otherOpCount = 0;
        int modifiedEntityId = -1;
        int modifiedDenseId = -1;
        var reader = new DeltaOpReader(stream);
        while (reader.TryReadOp(out var op))
        {
            if (op.Kind == DeltaOpKind.ComponentModify)
            {
                modifyCount++;
                modifiedEntityId = op.EntityId;
                modifiedDenseId = op.DenseId;
            }
            else
            {
                otherOpCount++;
            }
        }

        modifyCount.Should().Be(1, "only one entity's one component changed");
        otherOpCount.Should().Be(0, "no add/remove/create/destroy should be emitted");
        modifiedEntityId.Should().Be(entities[7].Id);
        modifiedDenseId.Should().Be(ComponentId<DeltaPosition>.IntId);

        // Sanity: full round-trip still converges.
        var applied = CloneWorld(baseline);
        DeltaApplier.ApplyOps(applied, stream);
        AssertWorldsEqual(world, applied);
    }

    // -- Helpers ---------------------------------------------------------------

    private static (EntityWorld world, EntityWorld applied) CreateMatchingWorlds()
    {
        var world = new EntityWorld();
        var applied = new EntityWorld();
        return (world, applied);
    }

    private static EntityWorld CloneWorld(EntityWorld source)
    {
        var clone = new EntityWorld();
        var bytes = StateSerializer.Serialize(source);
        StateSerializer.Deserialize(clone, bytes, syncComponentIds: false, autoReset: true, invalidateSystemData: false);
        return clone;
    }

    /// <summary>
    /// Bring <paramref name="applied"/> into byte-equivalence with <paramref name="world"/>
    /// using the delta pipeline. Both worlds start in matching state (fresh EntityWorld
    /// with just the World singleton entity), so a delta from <c>new EntityWorld()</c> to
    /// <paramref name="world"/> applied against <paramref name="applied"/> is correct.
    /// </summary>
    private static void SyncToApplied(EntityWorld world, EntityWorld applied)
    {
        var gen = new DeltaGenerator();
        var stream = gen.GenerateOpStream(world, new EntityWorld()).ToArray();
        DeltaApplier.ApplyOps(applied, stream);

        AssertWorldsEqual(world, applied);
    }

    private static void RoundTripAndAssertEqual(EntityWorld world, EntityWorld baseline, EntityWorld applied)
    {
        var gen = new DeltaGenerator();
        var stream = gen.GenerateOpStream(world, baseline).ToArray();
        DeltaApplier.ApplyOps(applied, stream);
        AssertWorldsEqual(world, applied);
    }

    private static void ApplyAndAdvance(DeltaGenerator gen, EntityWorld world, EntityWorld baseline, EntityWorld applied)
    {
        var stream = gen.GenerateOpStream(world, baseline).ToArray();
        DeltaApplier.ApplyOps(applied, stream);

        // Mirror DeltaSyncStrategyServer: baseline catches up to current state so the next
        // delta is incremental.
        var bytes = StateSerializer.Serialize(world);
        StateSerializer.Deserialize(baseline, bytes, syncComponentIds: false, autoReset: true, invalidateSystemData: false);
    }

    private static void AssertWorldsEqual(EntityWorld expected, EntityWorld actual)
    {
        var e = StateSerializer.Serialize(expected);
        var a = StateSerializer.Serialize(actual);
        a.Should().Equal(e);
    }

    // -- DeltaSubtractor tests -------------------------------------------------

    /// <summary>
    /// Simulates player B receiving a server delta that contains an EntityCreate for an entity
    /// B's own sim never spawned (e.g. player A's building). The TargetDelta produced by Subtract
    /// must contain that EntityCreate so applying it makes the entity appear on B.
    /// </summary>
    [Fact]
    public void Subtract_NonPredictedServerEntityCreate_EmittedInTargetDelta()
    {
        // Build server's per-tick delta: EntityCreate(100, DeltaPosition{10,20,30}).
        var serverBaseline = new EntityWorld();
        var serverCurrent = new EntityWorld();
        // Align ids: create fillers so server entity is at id 100.
        for (int i = serverCurrent.NextEntityId; i < 100; i++) serverCurrent.CreateEntity();
        var building = serverCurrent.CreateEntity();
        serverCurrent.AddComponent(building, new DeltaPosition { X = 10, Y = 20, Z = 30 });
        // Seed baseline to match server pre-create state.
        for (int i = serverBaseline.NextEntityId; i < 100; i++) serverBaseline.CreateEntity();

        var gen = new DeltaGenerator();
        var serverDelta = gen.GenerateOpStream(serverCurrent, serverBaseline).ToArray();

        // Client B's per-tick delta: empty (B simulated nothing interesting this tick).
        var clientDelta = ReadOnlySpan<byte>.Empty;

        // Client B's baseline = same as server's (both agreed on pre-create state).
        var clientBaseline = serverBaseline;

        var targetWriter = new DeltaOpWriter();
        DeltaSubtractor.Subtract(serverDelta, clientDelta, clientBaseline, targetWriter);

        // TargetDelta must include an EntityCreate for entity 100 with the position.
        var targetBytes = targetWriter.ToArray();
        targetBytes.Length.Should().BeGreaterThan(0);

        // Applying the TargetDelta to a B-equivalent world should produce the building.
        var clientWorld = new EntityWorld();
        for (int i = clientWorld.NextEntityId; i < 100; i++) clientWorld.CreateEntity();
        DeltaApplier.ApplyOps(clientWorld, targetBytes);

        clientWorld.HasComponent<DeltaPosition>(building).Should().BeTrue("building should appear on B");
        clientWorld.GetComponent<DeltaPosition>(building).X.Should().Be(10);
        clientWorld.GetComponent<DeltaPosition>(building).Y.Should().Be(20);
    }

    /// <summary>
    /// When client and server agree byte-for-byte on a per-tick delta op (perfect prediction),
    /// TargetDelta must be empty — no snap, client's further-evolved state is preserved.
    /// </summary>
    [Fact]
    public void Subtract_IdenticalDeltas_ProducesEmptyTarget()
    {
        var worldA = new EntityWorld();
        var e = worldA.CreateEntity();
        worldA.AddComponent(e, new DeltaPosition { X = 5, Y = 5, Z = 5 });
        var baseline = new EntityWorld();

        var gen = new DeltaGenerator();
        var serverDelta = gen.GenerateOpStream(worldA, baseline).ToArray();

        // Client produced the same delta (bit-equal prediction).
        var clientDelta = gen.GenerateOpStream(worldA, baseline).ToArray();

        var target = new DeltaOpWriter();
        DeltaSubtractor.Subtract(serverDelta, clientDelta, baseline, target);

        target.Length.Should().Be(0, "perfect prediction must not generate any reconciliation ops");
    }

    /// <summary>
    /// Client predicted an EntityCreate server never endorsed (e.g. action rejected as TooOld).
    /// Target must revert with an EntityDestroy so the orphan ghost doesn't linger.
    /// </summary>
    [Fact]
    public void Subtract_ClientOnlyEntityCreate_ProducesRevertDestroy()
    {
        var baseline = new EntityWorld();

        var clientWorld = new EntityWorld();
        var ghost = clientWorld.CreateEntity();
        clientWorld.AddComponent(ghost, new DeltaPosition { X = 1, Y = 2, Z = 3 });

        var gen = new DeltaGenerator();
        var clientDelta = gen.GenerateOpStream(clientWorld, baseline).ToArray();

        // Server had nothing to say — empty delta.
        var serverDelta = ReadOnlySpan<byte>.Empty;

        var target = new DeltaOpWriter();
        DeltaSubtractor.Subtract(serverDelta, clientDelta, baseline, target);

        // Apply revert to the client's world — the ghost entity should be destroyed.
        DeltaApplier.ApplyOps(clientWorld, target.WrittenSpan);
        clientWorld.HasComponent<DeltaPosition>(ghost).Should().BeFalse("orphan predicted entity must be reverted");
    }
}
