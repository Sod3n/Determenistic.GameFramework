using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics2D.Systems;

public class RapierPhysicsSystem : ISystem, IDisposable
{
    // Scratch lists are ThreadStatic so concurrent game loops don't clobber each other
    [ThreadStatic] private static List<Entity>? _entityBuffer;
    [ThreadStatic] private static List<int>? _intBuffer;

    private static List<Entity> EntityBuffer => _entityBuffer ??= new();
    private static List<int> IntBuffer => _intBuffer ??= new();

    static RapierPhysicsSystem()
    {
        RapierNativeLoader.Initialize();
    }

    public RapierPhysicsSystem()
    {
    }

    public void Update(EntityWorld state)
    {
        var action = PrepareStep(state);
        action?.Invoke();
        ApplyStep(state);
    }

    public Action? PrepareStep(EntityWorld state)
    {
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        if (physicsState == null)
        {
            physicsState = new RapierPhysicsState();
            state.SetCustomData(physicsState);
            physicsState.World = new RapierWorld();
        }

        var gameTime = state.GetCustomData<IGameTime>();
        if (gameTime == null) return null;

        // Dispose any old world deferred from the previous tick
        if (physicsState.PendingDispose != null)
        {
            physicsState.PendingDispose.Dispose();
            physicsState.PendingDispose = null;
        }

        // Detect Rollback / Initialization / Jump Forward
        if (physicsState.World == null || gameTime.CurrentTick != physicsState.LastSimulatedTick + 1)
        {
            RebuildWorld(state, physicsState);
        }

        // Prune Bodies (Remove destroyed entities)
        PruneBodies(state, physicsState);

        // Sync ECS changes to Physics (Creation/Destruction)
        SyncEcsToPhysics(state, physicsState);

        // Step Characters (Kinematic Movement) before physics step — needs ECS access
        if (physicsState.World != null)
        {
            var dt = (float)gameTime.FixedDeltaTime;
            physicsState.CharacterProcessor.StepCharacters(state, physicsState.World, physicsState.EntityToBody, dt);
        }

        // Capture what Step needs into the closure — no instance fields involved
        var world = physicsState.World;
        if (world == null) return null;

        var stepDt = (float)gameTime.FixedDeltaTime;
        return () =>
        {
            var gravity = new RVector(0.0f, 0.0f);
            var integrationParams = new RIntegrationParameters(
                dt: stepDt,
                minCcdDt: stepDt / 100.0f,
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

            world.Step(gravity, integrationParams);
        };
    }

    public void ApplyStep(EntityWorld state)
    {
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        var gameTime = state.GetCustomData<IGameTime>();
        if (physicsState == null || gameTime == null) return;

        // Update Area2D Overlaps (needs both Rapier world and ECS)
        if (physicsState.World != null)
        {
            physicsState.AreaProcessor.UpdateAreaOverlaps(state, physicsState.World, physicsState.BodyToEntity);
        }

        // Sync Physics to ECS
        SyncPhysicsToEcs(state, physicsState);

        physicsState.LastSimulatedTick = gameTime.CurrentTick;
    }

    private static void RebuildWorld(EntityWorld state, RapierPhysicsState physicsState)
    {
        // Defer disposal of the old world until next PrepareStep (after the async Step completes).
        if (physicsState.World != null)
        {
            physicsState.PendingDispose?.Dispose();
            physicsState.PendingDispose = physicsState.World;
            physicsState.World = null;
            physicsState.EntityToBody.Clear();
            physicsState.BodyToEntity.Clear();
            physicsState.CharacterProcessor.Clear();
        }

        // Always rebuild from ECS — ECS is the single source of truth
        physicsState.World = new RapierWorld();
        RebuildWorldFromECS(state, physicsState);
    }

    private static void RebuildWorldFromECS(EntityWorld state, RapierPhysicsState physicsState)
    {
        var entityBuffer = EntityBuffer;

        entityBuffer.Clear();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in entityBuffer)
            CreateBodyForEntity(state, physicsState, entity);

        entityBuffer.Clear();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in entityBuffer)
            CreateBodyForEntity(state, physicsState, entity);

        entityBuffer.Clear();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in entityBuffer)
            CreateBodyForEntity(state, physicsState, entity);

        entityBuffer.Clear();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in entityBuffer)
            CreateBodyForEntity(state, physicsState, entity);
    }

    private static void PruneBodies(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        var intBuffer = IntBuffer;

        // SORT FOR DETERMINISM: Dictionary iteration is undefined
        intBuffer.Clear();
        intBuffer.AddRange(physicsState.EntityToBody.Keys);
        intBuffer.Sort();

        int removeCount = 0;
        int bufferCount = intBuffer.Count;

        for (int idx = 0; idx < bufferCount; idx++)
        {
            int entityId = intBuffer[idx];
            var entity = new Entity(entityId);

            bool isValid = state.HasComponent<Transform2D>(entity) &&
                           (state.HasComponent<RigidBody2D>(entity) ||
                            state.HasComponent<StaticBody2D>(entity) ||
                            state.HasComponent<CharacterBody2D>(entity) ||
                            state.HasComponent<Area2D>(entity));

            if (!isValid)
            {
                intBuffer[removeCount] = entityId;
                removeCount++;
            }
        }

        for (int i = 0; i < removeCount; i++)
        {
            int entityId = intBuffer[i];
            ulong bodyHandle = physicsState.EntityToBody[entityId];

            physicsState.World.BodyDestroy(bodyHandle);

            physicsState.EntityToBody.Remove(entityId);
            physicsState.BodyToEntity.Remove(bodyHandle);
            physicsState.EntityToCollider.Remove(entityId);

            physicsState.CharacterProcessor.RemoveCharacter(entityId);
        }
    }

    private static void SyncEcsToPhysics(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        var entityBuffer = EntityBuffer;

        // 1. Dynamic Bodies
        entityBuffer.Clear();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in entityBuffer)
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

                SyncCollisionGroups(physicsState, entity.Id, body.CollisionLayer, body.CollisionMask);
            }
        }

        // 2. Static Bodies
        entityBuffer.Clear();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var body = ref state.GetComponent<StaticBody2D>(entity);
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);

                 SyncCollisionGroups(physicsState, entity.Id, body.CollisionLayer, body.CollisionMask);
             }
        }

        // 3. Character Bodies
        entityBuffer.Clear();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var body = ref state.GetComponent<CharacterBody2D>(entity);
                 ref var transform = ref state.GetComponent<Transform2D>(entity);

                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);

                 SyncCollisionGroups(physicsState, entity.Id, body.CollisionLayer, body.CollisionMask);
             }
        }

        // 4. Area2D
        entityBuffer.Clear();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) entityBuffer.Add(entity);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in entityBuffer)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 ref var area = ref state.GetComponent<Area2D>(entity);
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 physicsState.World?.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World?.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);

                 SyncCollisionGroups(physicsState, entity.Id, area.CollisionLayer, area.CollisionMask);
             }
        }
    }

    private static void CreateBodyForEntity(EntityWorld state, RapierPhysicsState physicsState, Entity entity)
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

        }
        else if (state.HasComponent<StaticBody2D>(entity))
        {
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.Fixed, translation, rotation);
        }
        else if (state.HasComponent<CharacterBody2D>(entity))
        {
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
        }
        else if (state.HasComponent<Area2D>(entity))
        {
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = physicsState.World.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
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
                physicsState.EntityToCollider[entity.Id] = colliderHandle;
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
                         ref var sb = ref state.GetComponent<StaticBody2D>(entity);
                         layer = sb.CollisionLayer;
                         mask = sb.CollisionMask;
                     }

                     physicsState.World.ColliderSetCollisionGroups(colliderHandle, layer, mask);
                }
            }
        }
    }

    private static void SyncPhysicsToEcs(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        var intBuffer = IntBuffer;

        // SORT FOR DETERMINISM: Dictionary iteration is undefined, so we must sort by ID.
        intBuffer.Clear();
        intBuffer.AddRange(physicsState.EntityToBody.Keys);
        intBuffer.Sort();

        foreach (var entityId in intBuffer)
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

    private static void SyncCollisionGroups(RapierPhysicsState physicsState, int entityId, uint layer, uint mask)
    {
        if (physicsState.World != null && physicsState.EntityToCollider.TryGetValue(entityId, out ulong colliderHandle))
        {
            physicsState.World.ColliderSetCollisionGroups(colliderHandle, layer, mask);
        }
    }

    public void Dispose()
    {
    }
}
