using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Deterministic.GameFramework.Navigation.Tests;

/// <summary>
/// Determinism tests for CDTNavigationSystem.
///
/// The game uses CDTNavigationSystem (not NavigationSystem), so these tests
/// cover the actual code path used in production. The critical scenario is
/// rollback + resimulation: after state restore, CDTNavigationState retains
/// its pre-rollback Map while NeedsStaleCheck triggers, which can cause
/// the bake logic to write different ECS values than forward simulation.
/// </summary>
[Collection("Sequential")]
public class CDTNavigationDeterminismTests
{
    private readonly ITestOutputHelper _output;

    public CDTNavigationDeterminismTests(ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem ts, CDTNavigationSystem ns) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(NavigationRegion2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationRegion2D>();
        state.RegisterComponent<NavigationAgent2D>();
        state.RegisterComponent<NavigationObstacle2D>();
        state.RegisterComponent<NavigationWorld2D>();
        state.RegisterComponent<StaticBody2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<CharacterBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<Area2D>();

        state.SetCustomData<IGameTime>(new FakeGameTime { CurrentTick = 0 });

        return (state, new TransformSystem(), new CDTNavigationSystem());
    }

    // CDTNavigationSystem reads obstacles from ECS components (StaticBody2D, Transform2D,
    // CollisionShape2D) — it does NOT need Rapier. Skipping RapierPhysicsSystem means
    // these tests run on any platform without native library dependencies.
    private void RunTick(EntityWorld state, TransformSystem ts, CDTNavigationSystem ns)
    {
        var gt = state.GetCustomData<IGameTime>() as FakeGameTime;
        gt!.CurrentTick++;
        ts.Update(state);
        ns.Update(state);
    }

    private void SetupScene(EntityWorld state)
    {
        // Nav world
        var nw = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-10, -10);
        world.BoundsMax = new Vector2(100, 50);
        world.CellSize = (Float)2;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)0; // no chunking, simpler for determinism tests
        state.AddComponent(nw, world);

        // Some obstacles
        for (int i = 0; i < 5; i++)
        {
            var wall = state.CreateEntity();
            state.AddComponent(wall, new Transform2D(new Vector2((Float)(10 + i * 15), (Float)20), 0, Vector2.One));
            state.AddComponent(wall, new StaticBody2D());
            state.AddComponent(wall, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));
        }

        // Agent with CharacterBody2D
        var agent = state.CreateEntity();
        state.AddComponent(agent, new Transform2D(new Vector2(5, 20), 0, Vector2.One));
        var body = CharacterBody2D.Default;
        body.CollisionLayer = 1;
        body.CollisionMask = uint.MaxValue;
        state.AddComponent(agent, body);
        state.AddComponent(agent, CollisionShape2D.CreateCircle((Float)0.5f));

        var nav = NavigationAgent2D.Default;
        nav.TargetPosition = new Vector2(80, 20);
        nav.IsNavigationFinished = false;
        nav.MaxSpeed = (Float)10;
        nav.PathDesiredDistance = (Float)1;
        nav.TargetDesiredDistance = (Float)2;
        nav.Radius = (Float)0.5f;
        nav.AvoidanceMask = 0;
        state.AddComponent(agent, nav);
    }

    private void ApplyNavVelocity(EntityWorld state)
    {
        foreach (var e in state.Filter<NavigationAgent2D, CharacterBody2D>())
        {
            ref var nav = ref state.GetComponent<NavigationAgent2D>(e);
            ref var b = ref state.GetComponent<CharacterBody2D>(e);
            b.Velocity = nav.Velocity;
        }
    }

    // ══════════════════════════════════════════════
    //  Test 1: Basic determinism — two identical forward sims
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_TwoIdenticalSimulations_ProduceSameHash()
    {
        var (stateA, tsA, nsA) = CreateWorld();
        SetupScene(stateA);
        for (int i = 0; i < 120; i++)
        {
            ApplyNavVelocity(stateA);
            RunTick(stateA, tsA, nsA);
        }

        var (stateB, tsB, nsB) = CreateWorld();
        SetupScene(stateB);
        for (int i = 0; i < 120; i++)
        {
            ApplyNavVelocity(stateB);
            RunTick(stateB, tsB, nsB);
        }

        var hashA = StateHasher.Hash(stateA);
        var hashB = StateHasher.Hash(stateB);

        _output.WriteLine($"Sim A: {hashA}");
        _output.WriteLine($"Sim B: {hashB}");

        hashA.Should().Be(hashB, "two identical CDT simulations must produce identical state");
    }

    // ══════════════════════════════════════════════
    //  Test 2: Snapshot restore — two fresh worlds from same snapshot
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_FromSameSnapshot_ProduceSameHash()
    {
        // Run source for 60 ticks
        var (source, tsS, nsS) = CreateWorld();
        SetupScene(source);
        for (int i = 0; i < 60; i++)
        {
            ApplyNavVelocity(source);
            RunTick(source, tsS, nsS);
        }

        byte[] snapshot = StateSerializer.Serialize(source);
        _output.WriteLine($"Snapshot hash at tick 60: {StateHasher.Hash(source)}");

        // Two fresh worlds from same snapshot
        var (simA, tsA, nsA) = CreateWorld();
        SetupScene(simA);
        StateSerializer.Deserialize(simA, snapshot, syncComponentIds: false);

        var (simB, tsB, nsB) = CreateWorld();
        SetupScene(simB);
        StateSerializer.Deserialize(simB, snapshot, syncComponentIds: false);

        // Run both for 60 more ticks
        for (int i = 0; i < 60; i++)
        {
            ApplyNavVelocity(simA);
            RunTick(simA, tsA, nsA);
            ApplyNavVelocity(simB);
            RunTick(simB, tsB, nsB);
        }

        var hashA = StateHasher.Hash(simA);
        var hashB = StateHasher.Hash(simB);

        _output.WriteLine($"Sim A: {hashA}");
        _output.WriteLine($"Sim B: {hashB}");

        hashA.Should().Be(hashB,
            "two CDT simulations from the same snapshot must produce identical state");
    }

    // ══════════════════════════════════════════════
    //  Test 3: Rollback — the critical desync scenario
    //
    //  Forward sim:  tick 0 ──────────────────────► tick 120
    //  Rollback sim: tick 0 ──► tick 60 (snap) ──► tick 80
    //                                   ╰── restore ──► tick 120
    //
    //  After restore, CDTNavigationState.Map retains tick-80
    //  geometry while ECS is at tick-60. NeedsStaleCheck fires.
    //  The final hash at tick 120 must match forward sim.
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_RollbackResim_ProducesSameHashAsForwardSim()
    {
        // === Forward reference sim: straight through to tick 120 ===
        var (fwdState, fwdTs, fwdNs) = CreateWorld();
        SetupScene(fwdState);
        for (int i = 0; i < 120; i++)
        {
            ApplyNavVelocity(fwdState);
            RunTick(fwdState, fwdTs, fwdNs);
        }
        var forwardHash = StateHasher.Hash(fwdState);
        _output.WriteLine($"Forward sim hash at tick 120: {forwardHash}");

        // === Rollback sim ===
        var (rbState, rbTs, rbNs) = CreateWorld();
        SetupScene(rbState);

        // Run to tick 60
        for (int i = 0; i < 60; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Snapshot at tick 60
        byte[] snapshot = StateSerializer.Serialize(rbState);
        _output.WriteLine($"Snapshot hash at tick 60: {StateHasher.Hash(rbState)}");

        // Continue forward to tick 80 — CDTNavigationState.Map advances
        for (int i = 0; i < 20; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }
        _output.WriteLine($"Pre-rollback hash at tick 80: {StateHasher.Hash(rbState)}");

        // Rollback: restore ECS to tick 60
        // CDTNavigationState is NOT serialized — Map retains tick-80 geometry
        // Deserialize calls InvalidateDerivedState(false) → NeedsStaleCheck = true
        StateSerializer.Deserialize(rbState, snapshot, syncComponentIds: false);

        // Reset tick counter (SystemData is not serialized, FakeGameTime persists)
        var gt = rbState.GetCustomData<IGameTime>() as FakeGameTime;
        gt!.CurrentTick = 60;

        _output.WriteLine($"After restore hash (should match tick 60): {StateHasher.Hash(rbState)}");

        // Resim from tick 61 to tick 120
        for (int i = 0; i < 60; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        var rollbackHash = StateHasher.Hash(rbState);
        _output.WriteLine($"Rollback sim hash at tick 120: {rollbackHash}");

        rollbackHash.Should().Be(forwardHash,
            "rollback + resimulation must produce identical state to forward simulation — " +
            "CDTNavigationSystem's NeedsStaleCheck must not cause ECS divergence");
    }

    // ══════════════════════════════════════════════
    //  Test 4: Rollback with obstacle added mid-session
    //
    //  Obstacle added at tick 30. Snapshot at tick 60 includes
    //  the obstacle. Rollback from tick 80 to tick 60.
    //  The stale check sees the obstacle in both the map and ECS.
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_RollbackAfterObstacleChange_ProducesSameHash()
    {
        // === Forward reference sim ===
        var (fwdState, fwdTs, fwdNs) = CreateWorld();
        SetupScene(fwdState);

        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(fwdState);
            RunTick(fwdState, fwdTs, fwdNs);
        }

        // Add obstacle at tick 30
        var fwdObstacle = fwdState.CreateEntity();
        fwdState.AddComponent(fwdObstacle, new Transform2D(new Vector2(40, 20), 0, Vector2.One));
        fwdState.AddComponent(fwdObstacle, new StaticBody2D());
        fwdState.AddComponent(fwdObstacle, CollisionShape2D.CreateRectangle(new Vector2(3, 3)));

        for (int i = 0; i < 90; i++)
        {
            ApplyNavVelocity(fwdState);
            RunTick(fwdState, fwdTs, fwdNs);
        }
        var forwardHash = StateHasher.Hash(fwdState);
        _output.WriteLine($"Forward hash at tick 120: {forwardHash}");

        // === Rollback sim ===
        var (rbState, rbTs, rbNs) = CreateWorld();
        SetupScene(rbState);

        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Add same obstacle at tick 30
        var rbObstacle = rbState.CreateEntity();
        rbState.AddComponent(rbObstacle, new Transform2D(new Vector2(40, 20), 0, Vector2.One));
        rbState.AddComponent(rbObstacle, new StaticBody2D());
        rbState.AddComponent(rbObstacle, CollisionShape2D.CreateRectangle(new Vector2(3, 3)));

        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Snapshot at tick 60 (obstacle is present in the snapshot)
        byte[] snapshot = StateSerializer.Serialize(rbState);

        // Continue to tick 80
        for (int i = 0; i < 20; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Rollback to tick 60
        StateSerializer.Deserialize(rbState, snapshot, syncComponentIds: false);
        var gt = rbState.GetCustomData<IGameTime>() as FakeGameTime;
        gt!.CurrentTick = 60;

        // Resim to tick 120
        for (int i = 0; i < 60; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        var rollbackHash = StateHasher.Hash(rbState);
        _output.WriteLine($"Rollback hash at tick 120: {rollbackHash}");

        rollbackHash.Should().Be(forwardHash,
            "rollback after obstacle change must produce identical state — " +
            "NeedsStaleCheck should rebuild to the same result as forward simulation");
    }

    // ══════════════════════════════════════════════
    //  Test 5: Rollback across an obstacle change
    //
    //  Obstacle added at tick 40 (AFTER the snapshot at tick 30).
    //  Rollback from tick 80 to tick 30 (before obstacle existed).
    //  Resim replays the obstacle add at tick 40 and continues.
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_RollbackAcrossObstacleChange_ProducesSameHash()
    {
        // === Forward reference sim ===
        var (fwdState, fwdTs, fwdNs) = CreateWorld();
        SetupScene(fwdState);

        for (int i = 0; i < 120; i++)
        {
            // Add obstacle at tick 40
            if ((fwdState.GetCustomData<IGameTime>() as FakeGameTime)!.CurrentTick == 39)
            {
                var obs = fwdState.CreateEntity();
                fwdState.AddComponent(obs, new Transform2D(new Vector2(50, 25), 0, Vector2.One));
                fwdState.AddComponent(obs, new StaticBody2D());
                fwdState.AddComponent(obs, CollisionShape2D.CreateRectangle(new Vector2(4, 4)));
            }
            ApplyNavVelocity(fwdState);
            RunTick(fwdState, fwdTs, fwdNs);
        }
        var forwardHash = StateHasher.Hash(fwdState);
        _output.WriteLine($"Forward hash at tick 120: {forwardHash}");

        // === Rollback sim ===
        var (rbState, rbTs, rbNs) = CreateWorld();
        SetupScene(rbState);

        // Run to tick 30 (before obstacle)
        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Snapshot at tick 30
        byte[] snapshot = StateSerializer.Serialize(rbState);

        // Continue to tick 80, adding obstacle at tick 40
        for (int i = 0; i < 50; i++)
        {
            if ((rbState.GetCustomData<IGameTime>() as FakeGameTime)!.CurrentTick == 39)
            {
                var obs = rbState.CreateEntity();
                rbState.AddComponent(obs, new Transform2D(new Vector2(50, 25), 0, Vector2.One));
                rbState.AddComponent(obs, new StaticBody2D());
                rbState.AddComponent(obs, CollisionShape2D.CreateRectangle(new Vector2(4, 4)));
            }
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Rollback to tick 30 (before obstacle existed)
        // CDTNavigationState.Map has post-obstacle geometry from tick 80
        StateSerializer.Deserialize(rbState, snapshot, syncComponentIds: false);
        var gt = rbState.GetCustomData<IGameTime>() as FakeGameTime;
        gt!.CurrentTick = 30;

        // Resim to tick 120, replaying obstacle add at tick 40
        for (int i = 0; i < 90; i++)
        {
            if (gt.CurrentTick == 39)
            {
                var obs = rbState.CreateEntity();
                rbState.AddComponent(obs, new Transform2D(new Vector2(50, 25), 0, Vector2.One));
                rbState.AddComponent(obs, new StaticBody2D());
                rbState.AddComponent(obs, CollisionShape2D.CreateRectangle(new Vector2(4, 4)));
            }
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        var rollbackHash = StateHasher.Hash(rbState);
        _output.WriteLine($"Rollback hash at tick 120: {rollbackHash}");

        rollbackHash.Should().Be(forwardHash,
            "rollback across obstacle change must produce identical state — " +
            "NeedsStaleCheck detects stale map, rebuilds, and result matches forward sim");
    }

    // ══════════════════════════════════════════════
    //  Test 6: Multiple rollbacks — stress the NeedsStaleCheck path
    // ══════════════════════════════════════════════

    [Fact]
    public void CDT_MultipleRollbacks_ProduceSameHash()
    {
        // === Forward reference sim ===
        var (fwdState, fwdTs, fwdNs) = CreateWorld();
        SetupScene(fwdState);
        for (int i = 0; i < 120; i++)
        {
            ApplyNavVelocity(fwdState);
            RunTick(fwdState, fwdTs, fwdNs);
        }
        var forwardHash = StateHasher.Hash(fwdState);
        _output.WriteLine($"Forward hash at tick 120: {forwardHash}");

        // === Rollback sim: multiple rollbacks ===
        var (rbState, rbTs, rbNs) = CreateWorld();
        SetupScene(rbState);
        var gt = rbState.GetCustomData<IGameTime>() as FakeGameTime;

        // Run to tick 30, snapshot
        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }
        byte[] snap30 = StateSerializer.Serialize(rbState);

        // Continue to tick 50, snapshot
        for (int i = 0; i < 20; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }
        byte[] snap50 = StateSerializer.Serialize(rbState);

        // Continue to tick 70
        for (int i = 0; i < 20; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Rollback #1: restore to tick 50
        StateSerializer.Deserialize(rbState, snap50, syncComponentIds: false);
        gt!.CurrentTick = 50;

        // Resim to tick 80
        for (int i = 0; i < 30; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        // Rollback #2: restore to tick 30
        StateSerializer.Deserialize(rbState, snap30, syncComponentIds: false);
        gt.CurrentTick = 30;

        // Resim to tick 120
        for (int i = 0; i < 90; i++)
        {
            ApplyNavVelocity(rbState);
            RunTick(rbState, rbTs, rbNs);
        }

        var rollbackHash = StateHasher.Hash(rbState);
        _output.WriteLine($"Multi-rollback hash at tick 120: {rollbackHash}");

        rollbackHash.Should().Be(forwardHash,
            "multiple rollbacks must converge to the same state as forward simulation");
    }
}
