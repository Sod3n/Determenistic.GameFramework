using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics2D.Systems;

public class RapierPhysicsSystem : IAsyncSystem, IDisposable
{
    // State held between SyncFrom → Step → SyncTo within a single tick
    private RapierPhysicsState? _currentPhysicsState;
    private IGameTime? _currentGameTime;
    private EntityWorld? _currentState;

    // Reusable scratch lists to avoid per-tick allocations
    private readonly List<Entity> _entityBuffer = new();
    private readonly List<int> _intBuffer = new();

    static RapierPhysicsSystem()
    {
        RapierNativeLoader.Initialize();
    }

#if NETCOREAPP3_0_OR_GREATER
#endif

    public RapierPhysicsSystem()
    {
    }

    public void Update(EntityWorld state)
    {
        SyncFrom(state);
        Step();
        SyncTo(state);
    }

    public void SyncFrom(EntityWorld state)
    {
        _currentState = state;
        _currentPhysicsState = state.GetCustomData<RapierPhysicsState>();
        if (_currentPhysicsState == null)
        {
            _currentPhysicsState = new RapierPhysicsState();
            state.SetCustomData(_currentPhysicsState);
            _currentPhysicsState.World = new RapierWorld();
        }

        _currentGameTime = state.GetCustomData<IGameTime>();
        if (_currentGameTime == null)
        {
            _currentPhysicsState = null;
            return;
        }

        // Detect Rollback / Initialization / Jump Forward
        if (_currentPhysicsState.World == null || _currentGameTime.CurrentTick != _currentPhysicsState.LastSimulatedTick + 1)
        {
            RebuildWorld(state, _currentPhysicsState);
        }

        // Prune Bodies (Remove destroyed entities)
        PruneBodies(state, _currentPhysicsState);

        // Sync ECS changes to Physics (Creation/Destruction)
        SyncEcsToPhysics(state, _currentPhysicsState);

        // Step Characters (Kinematic Movement) before physics step — needs ECS access
        if (_currentPhysicsState.World != null)
        {
            var dt = (float)_currentGameTime.FixedDeltaTime;
            _currentPhysicsState.CharacterProcessor.StepCharacters(state, _currentPhysicsState.World, _currentPhysicsState.EntityToBody, dt);
        }
    }

    public void Step()
    {
        if (_currentPhysicsState?.World == null || _currentGameTime == null) return;

        var dt = (float)_currentGameTime.FixedDeltaTime;
        var gravity = new RVector(0.0f, 0.0f);

        var integrationParams = new RIntegrationParameters(
            dt: dt,
            minCcdDt: dt / 100.0f,
            lengthUnit: 1.0f,
            warmstartCoefficient: 0.5f,
            contactNaturalFrequency: 30.0f,
            contactDampingRatio: 1.0f,
            normalizedAllowedLinearError: 0.001f,
            normalizedMaxCorrectiveVelocity: 10.0f,
            normalizedPredictionDistance: 0.002f,
            numSolverIterations: 4,
            numInternalPgsIterations: 1,
            numInternalStabilizationIterations: 1,
            minIslandSize: 128,
            maxCcdSubsteps: 1
        );

        _currentPhysicsState.World.Step(gravity, integrationParams);
    }

    public void SyncTo(EntityWorld state)
    {
        if (_currentPhysicsState == null || _currentGameTime == null) return;

        // Update Area2D Overlaps (needs both Rapier world and ECS)
        if (_currentPhysicsState.World != null)
        {
            _currentPhysicsState.AreaProcessor.UpdateAreaOverlaps(state, _currentPhysicsState.World, _currentPhysicsState.BodyToEntity);
        }

        // Sync Physics to ECS
        SyncPhysicsToEcs(state, _currentPhysicsState);

        _currentPhysicsState.LastSimulatedTick = _currentGameTime.CurrentTick;

        // Clear per-tick references
        _currentState = null;
        _currentPhysicsState = null;
        _currentGameTime = null;
    }

    private void RebuildWorld(EntityWorld state, RapierPhysicsState physicsState)
    {
        // Dispose existing world
        if (physicsState.World != null)
        {
            physicsState.World.Dispose();
            physicsState.World = null;
            physicsState.EntityToBody.Clear();
            physicsState.BodyToEntity.Clear();
            physicsState.CharacterProcessor.Clear();
        }

        // Always rebuild from ECS — ECS is the single source of truth
        physicsState.World = new RapierWorld();
        RebuildWorldFromECS(state, physicsState);
    }

    private void RebuildWorldFromECS(EntityWorld state, RapierPhysicsState physicsState)
    {
        _entityBuffer.Clear();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in _entityBuffer)
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }

        _entityBuffer.Clear();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in _entityBuffer)
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }

        _entityBuffer.Clear();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in _entityBuffer)
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }

        _entityBuffer.Clear();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in _entityBuffer)
        {
            ref var body = ref state.GetComponent<Area2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }
    }

    private void PruneBodies(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        // SORT FOR DETERMINISM: Dictionary iteration is undefined
        _intBuffer.Clear();
        _intBuffer.AddRange(physicsState.EntityToBody.Keys);
        _intBuffer.Sort();

        // Collect entities to remove (reuse _longBuffer as int-compatible scratch — use _entityBuffer instead)
        // We need a second buffer here; use _entityBuffer.Count as a trick — but simplest is just to
        // remove in reverse after collecting indices. We'll collect into _longBuffer reinterpreted.
        int removeCount = 0;

        foreach (var entityId in _intBuffer)
        {
            var entity = new Entity(entityId);

            bool isValid = state.HasComponent<Transform2D>(entity) &&
                           (state.HasComponent<RigidBody2D>(entity) ||
                            state.HasComponent<StaticBody2D>(entity) ||
                            state.HasComponent<CharacterBody2D>(entity) ||
                            state.HasComponent<Area2D>(entity));

            if (!isValid)
            {
                // Mark for removal by swapping to front of _intBuffer
                // Actually, just overwrite from the start since we won't iterate _intBuffer again
                _intBuffer[removeCount] = entityId;
                removeCount++;
            }
        }

        for (int i = 0; i < removeCount; i++)
        {
            int entityId = _intBuffer[i];
            ulong bodyHandle = physicsState.EntityToBody[entityId];

            physicsState.World.BodyDestroy(bodyHandle);

            physicsState.EntityToBody.Remove(entityId);
            physicsState.BodyToEntity.Remove(bodyHandle);

            physicsState.CharacterProcessor.RemoveCharacter(entityId);
        }
    }

    private void SyncEcsToPhysics(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        // 1. Dynamic Bodies
        _entityBuffer.Clear();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in _entityBuffer)
        {
            if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
            {
                CreateBodyForEntity(state, physicsState, entity);
            }
            else
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                ref var transform = ref state.GetComponent<Transform2D>(entity);

                // FORCE SYNC for determinism: Always snap Physics to ECS state at start of frame.
                physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);

                physicsState.World.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
                physicsState.World.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);
            }
        }

        // 2. Static Bodies
        _entityBuffer.Clear();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in _entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }

        // 3. Character Bodies
        _entityBuffer.Clear();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in _entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var transform = ref state.GetComponent<Transform2D>(entity);

                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }

        // 4. Area2D
        _entityBuffer.Clear();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) _entityBuffer.Add(entity);
        _entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in _entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 physicsState.World?.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World?.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }
    }

    private void CreateBodyForEntity(EntityWorld state, RapierPhysicsState physicsState, Entity entity)
    {
        if (physicsState.World == null) return;

        ref var transform = ref state.GetComponent<Transform2D>(entity);

        ulong bodyHandle = 0;

        if (state.HasComponent<RigidBody2D>(entity))
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.Dynamic, translation, rotation);

            physicsState.World.BodySetMass(bodyHandle, (float)body.Mass, true);
            physicsState.World.BodySetGravityScale(bodyHandle, (float)body.GravityScale, true);
            physicsState.World.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
            physicsState.World.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);

            physicsState.World.BodySetLinearDamping(bodyHandle, (float)body.LinearDamping);
            physicsState.World.BodySetAngularDamping(bodyHandle, (float)body.AngularDamping);
            physicsState.World.BodySetCcdEnabled(bodyHandle, body.CcdEnabled);

            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<StaticBody2D>(entity))
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.Fixed, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<CharacterBody2D>(entity))
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<Area2D>(entity))
        {
            ref var body = ref state.GetComponent<Area2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
            body.BodyId = bodyHandle;
        }

        physicsState.EntityToBody[entity.Id] = bodyHandle;
        physicsState.BodyToEntity[bodyHandle] = entity.Id;

        // Create Collider if present
        if (state.HasComponent<CollisionShape2D>(entity))
        {
            ref var shapeComp = ref state.GetComponent<CollisionShape2D>(entity);
            RapierShape? shape = null;

            if (shapeComp.Type == CollisionShapeType.Circle)
            {
                shape = RapierShape.Ball((float)shapeComp.Circle.Radius);
            }
            else if (shapeComp.Type == CollisionShapeType.Rectangle)
            {
                shape = RapierShape.Cuboid((float)shapeComp.Rectangle.Size.X / 2.0f, (float)shapeComp.Rectangle.Size.Y / 2.0f);
            }
            else if (shapeComp.Type == CollisionShapeType.Capsule)
            {
                shape = RapierShape.Capsule((float)shapeComp.Capsule.Height / 2.0f, (float)shapeComp.Capsule.Radius);
            }

            if (shape != null)
            {
                var colTranslation = new RVector((float)shapeComp.Position.X, (float)shapeComp.Position.Y);
                var colRotation = new RRotation((float)shapeComp.Rotation);

                ulong colliderHandle = physicsState.World.ColliderCreate(shape, bodyHandle, colTranslation, colRotation, 0.5f, 0.0f);
                shape.Dispose();

                // Special handling for Area2D
                if (state.HasComponent<Area2D>(entity))
                {
                    ref var area = ref state.GetComponent<Area2D>(entity);
                    physicsState.World.ColliderSetSensor(colliderHandle, true);

                    physicsState.World.ColliderSetCollisionGroups(colliderHandle, area.CollisionLayer, area.CollisionMask);
                }
                else
                {
                     uint layer = 0xFFFF0001;
                     uint mask = 0xFFFF0001;

                     if (state.HasComponent<RigidBody2D>(entity))
                     {
                         ref var rb = ref state.GetComponent<RigidBody2D>(entity);
                         layer = rb.CollisionLayer;
                         mask = rb.CollisionMask;
                     }
                     else if (state.HasComponent<CharacterBody2D>(entity))
                     {
                         ref var cb = ref state.GetComponent<CharacterBody2D>(entity);
                         layer = cb.CollisionLayer;
                         mask = cb.CollisionMask;
                     }
                     else if (state.HasComponent<StaticBody2D>(entity))
                     {
                     }

                     physicsState.World.ColliderSetCollisionGroups(colliderHandle, layer, mask);
                }
            }
        }
    }

    private void SyncPhysicsToEcs(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        // SORT FOR DETERMINISM: Dictionary iteration is undefined, so we must sort by ID.
        _intBuffer.Clear();
        _intBuffer.AddRange(physicsState.EntityToBody.Keys);
        _intBuffer.Sort();

        foreach (var entityId in _intBuffer)
        {
            ulong bodyHandle = physicsState.EntityToBody[entityId];
            var entity = new Entity(entityId);

            if (!state.HasComponent<Transform2D>(entity)) continue;

            if (state.HasComponent<RigidBody2D>(entity))
            {
                var rapierPos = physicsState.World.BodyGetTranslation(bodyHandle);
                var rapierRot = physicsState.World.BodyGetRotation(bodyHandle);

                ref var transform = ref state.GetComponent<Transform2D>(entity);
                transform.GlobalPosition = new Vector2(rapierPos.x, rapierPos.y);
                transform.GlobalRotation = rapierRot.angle;

                var rapierVel = physicsState.World.BodyGetLinvel(bodyHandle);
                var rapierAngVel = physicsState.World.BodyGetAngvel(bodyHandle);

                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                body.LinearVelocity = new Vector2(rapierVel.x, rapierVel.y);
                body.AngularVelocity = rapierAngVel.x;
            }
        }
    }

    public void Dispose()
    {
    }
}
