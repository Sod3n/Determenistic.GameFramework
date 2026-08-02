using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Box2D;
using static Deterministic.GameFramework.Box2D.B2Types;
using static Deterministic.GameFramework.Box2D.B2Worlds;
using static Deterministic.GameFramework.Box2D.B2Bodies;
using static Deterministic.GameFramework.Box2D.B2Shapes;
using static Deterministic.GameFramework.Box2D.B2Geometries;
using static Deterministic.GameFramework.Box2D.B2MathFunction;

using Deterministic.GameFramework.Utils.Logging;
using Float = Deterministic.GameFramework.Types.Float;

namespace Deterministic.GameFramework.Physics2D.Systems;

/// <summary>
/// Box2D-based physics system using deterministic Fixed64 math.
/// Drop-in replacement for RapierPhysicsSystem — reads the same ECS components
/// (StaticBody2D, RigidBody2D, CharacterBody2D, CollisionShape2D, Area2D).
///
/// Unlike Rapier, Box2D with Fixed64 is fully deterministic, so the world is
/// persistent (not rebuilt every frame). On rollback, save/restore world state.
/// </summary>
[ManualSystem]
public class Box2DPhysicsSystem : ISystem
{
    [ThreadStatic] private static List<Entity>? _entityBuffer;
    private static List<Entity> EntityBuffer => _entityBuffer ??= new();

    public void Update(EntityWorld state)
    {
        var physState = state.GetCustomData<Box2DPhysicsState>();
        if (physState == null)
        {
            physState = new Box2DPhysicsState();
            state.SetCustomData(physState);
        }

        var gameTime = state.GetCustomData<IGameTime>();
        if (gameTime == null) return;

        // After rollback/state sync: destroy and recreate the world.
        // This ensures no stale bodies/contacts/solver state persist.
        if (physState._pendingPrune)
        {
            physState._pendingPrune = false;
            if (physState.WorldId.index1 != 0)
            {
                b2DestroyWorld(physState.WorldId);
                physState.WorldId = default;
            }
            physState.EntityToBody.Clear();
            physState.BodyIndexToEntity.Clear();
        }

        // Ensure world exists
        if (physState.WorldId.index1 == 0)
        {
            var worldDef = b2DefaultWorldDef();
            worldDef.gravity = new B2Vec2(0, 0); // top-down 2D, no gravity
            physState.WorldId = b2CreateWorld(worldDef);
        }

        // Sync ECS → Box2D (create/update/remove bodies)
        SyncEcsToBox2D(state, physState);

        // Step characters (kinematic movement)
        StepCharacters(state, physState, (float)gameTime.FixedDeltaTime);

        // Step physics
        b2World_Step(physState.WorldId, (float)gameTime.FixedDeltaTime, 4);

        // Sync Box2D → ECS (read back positions/velocities)
        SyncBox2DToEcs(state, physState);
        SyncCharactersFromBox2D(state, physState);

        // Process Area2D sensor overlaps
        ProcessSensorEvents(state, physState);
    }

    // ══════════════════════════════════════════════
    //  ECS → Box2D sync
    // ══════════════════════════════════════════════

    private static void SyncEcsToBox2D(EntityWorld state, Box2DPhysicsState physState)
    {
        var entityBuffer = EntityBuffer;
        entityBuffer.Clear();

        // Collect all physics entities
        foreach (var e in state.Filter<Transform2D, CollisionShape2D>())
            entityBuffer.Add(e);
        entityBuffer.Sort((a, b) => a.Id.CompareTo(b.Id));

        // Track which entities still exist (for pruning)
        var alive = new HashSet<int>();

        foreach (var entity in entityBuffer)
        {
            alive.Add(entity.Id);

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shape = ref state.GetComponent<CollisionShape2D>(entity);
            if (shape.Disabled) continue;

            bool exists = physState.EntityToBody.ContainsKey(entity.Id);

            if (!exists)
            {
                // Create body
                var bodyDef = b2DefaultBodyDef();
                bodyDef.position = ToB2(transform.GlobalPosition);
                bodyDef.rotation = b2MakeRot((float)transform.GlobalRotation);

                if (state.HasComponent<RigidBody2D>(entity))
                {
                    ref var rb = ref state.GetComponent<RigidBody2D>(entity);
                    bodyDef.type = B2BodyType.b2_dynamicBody;
                    bodyDef.linearVelocity = ToB2(rb.LinearVelocity);
                    bodyDef.gravityScale = (float)rb.GravityScale;
                    bodyDef.linearDamping = (float)rb.LinearDamping;
                    bodyDef.angularDamping = (float)rb.AngularDamping;
                    // Box2D v3: lock rotation via body flags after creation if needed
                }
                else if (state.HasComponent<CharacterBody2D>(entity))
                {
                    // Use dynamic body so Box2D solver handles collision with obstacles.
                    // Velocity is set each frame from CharacterBody2D.Velocity.
                    bodyDef.type = B2BodyType.b2_dynamicBody;
                    bodyDef.gravityScale = 0; // no gravity for top-down characters
                    bodyDef.linearDamping = 0;
                    bodyDef.angularDamping = 100; // prevent spinning
                }
                else if (state.HasComponent<Area2D>(entity))
                {
                    // Dynamic so Box2D generates sensor events (kinematic sensors
                    // don't detect static bodies). Zero gravity + high damping = no drift.
                    bodyDef.type = B2BodyType.b2_dynamicBody;
                    bodyDef.gravityScale = 0;
                    bodyDef.linearDamping = 100;
                    bodyDef.angularDamping = 100;
                }
                else
                {
                    bodyDef.type = B2BodyType.b2_staticBody;
                }

                var bodyId = b2CreateBody(physState.WorldId, bodyDef);
                physState.EntityToBody[entity.Id] = bodyId;
                physState.BodyIndexToEntity[bodyId.index1] = entity.Id;

                // Create shape
                var shapeDef = b2DefaultShapeDef();

                // Collision filtering
                uint layer = 1, mask = uint.MaxValue;
                if (state.HasComponent<StaticBody2D>(entity))
                {
                    var sb = state.GetComponent<StaticBody2D>(entity);
                    layer = sb.CollisionLayer; mask = sb.CollisionMask;
                }
                else if (state.HasComponent<RigidBody2D>(entity))
                {
                    var rb = state.GetComponent<RigidBody2D>(entity);
                    layer = rb.CollisionLayer; mask = rb.CollisionMask;
                }
                else if (state.HasComponent<CharacterBody2D>(entity))
                {
                    var cb = state.GetComponent<CharacterBody2D>(entity);
                    layer = cb.CollisionLayer; mask = cb.CollisionMask;
                }

                shapeDef.filter = new B2Filter(layer, mask, 0);

                // Enable sensor events on ALL shapes so Area2D sensors can detect them.
                // In Box2D v3, both the sensor AND the target shape need this flag.
                shapeDef.enableSensorEvents = true;

                if (state.HasComponent<Area2D>(entity))
                    shapeDef.isSensor = true;

                CreateBox2DShape(bodyId, shapeDef, ref shape);
            }
            else
            {
                // Update existing body
                var bodyId = physState.EntityToBody[entity.Id];

                // Sync transform
                b2Body_SetTransform(bodyId, ToB2(transform.GlobalPosition),
                    b2MakeRot((float)transform.GlobalRotation));

                // Sync velocity for dynamic bodies
                if (state.HasComponent<RigidBody2D>(entity))
                {
                    ref var rb = ref state.GetComponent<RigidBody2D>(entity);
                    b2Body_SetLinearVelocity(bodyId, ToB2(rb.LinearVelocity));
                }

                // Keep Area2D bodies awake so sensor events fire
                if (state.HasComponent<Area2D>(entity))
                {
                    b2Body_SetAwake(bodyId, true);
                }
            }
        }

        // Prune removed entities
        var toRemove = new List<int>();
        foreach (var kvp in physState.EntityToBody)
        {
            if (!alive.Contains(kvp.Key))
                toRemove.Add(kvp.Key);
        }
        foreach (var id in toRemove)
        {
            var bodyId = physState.EntityToBody[id];
            physState.BodyIndexToEntity.Remove(bodyId.index1);
            b2DestroyBody(bodyId);
            physState.EntityToBody.Remove(id);
        }
    }

    private static void CreateBox2DShape(B2BodyId bodyId, B2ShapeDef shapeDef, ref CollisionShape2D shape)
    {
        if (shape.Type == CollisionShapeType.Circle)
        {
            var circle = new B2Circle(
                new B2Vec2((float)shape.Position.X, (float)shape.Position.Y),
                (float)shape.Circle.Radius);
            b2CreateCircleShape(bodyId, shapeDef, circle);
        }
        else if (shape.Type == CollisionShapeType.Rectangle)
        {
            var halfW = (float)shape.Rectangle.Size.X * 0.5f;
            var halfH = (float)shape.Rectangle.Size.Y * 0.5f;
            var box = b2MakeOffsetBox(halfW, halfH,
                new B2Vec2((float)shape.Position.X, (float)shape.Position.Y),
                b2MakeRot((float)shape.Rotation));
            b2CreatePolygonShape(bodyId, shapeDef, box);
        }
        else if (shape.Type == CollisionShapeType.Capsule)
        {
            var r = (float)shape.Capsule.Radius;
            var h = (float)shape.Capsule.Height * 0.5f;
            var capsule = new B2Capsule(
                new B2Vec2((float)shape.Position.X, (float)shape.Position.Y - h + r),
                new B2Vec2((float)shape.Position.X, (float)shape.Position.Y + h - r),
                r);
            b2CreateCapsuleShape(bodyId, shapeDef, capsule);
        }
    }

    // ══════════════════════════════════════════════
    //  Character stepping (kinematic bodies)
    // ══════════════════════════════════════════════

    private static void StepCharacters(EntityWorld state, Box2DPhysicsState physState, float dt)
    {
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>())
        {
            if (!physState.EntityToBody.TryGetValue(entity.Id, out var bodyId))
                continue;

            ref var charBody = ref state.GetComponent<CharacterBody2D>(entity);

            // Set velocity on the dynamic body — Box2D solver handles collision
            b2Body_SetLinearVelocity(bodyId, ToB2(charBody.Velocity));
        }
    }

    private static void SyncCharactersFromBox2D(EntityWorld state, Box2DPhysicsState physState)
    {
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>())
        {
            if (!physState.EntityToBody.TryGetValue(entity.Id, out var bodyId))
                continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var charBody = ref state.GetComponent<CharacterBody2D>(entity);

            var pos = b2Body_GetPosition(bodyId);
            var vel = b2Body_GetLinearVelocity(bodyId);
            var newPos = FromB2(pos);

            // Detect stuck character: has velocity but didn't move (trapped in obstacle).
            // Allow them to pass through by applying velocity directly.
            if (charBody.Velocity.SqrMagnitude > (Float)0.01f)
            {
                var moved = Vector2.DistanceSquared(newPos, transform.GlobalPosition);
                if ((float)moved < 0.001f)
                {
                    // Stuck — bypass physics, move directly
                    var gameTime = state.GetCustomData<IGameTime>();
                    Float dt = gameTime != null ? gameTime.FixedDeltaTime : (Float)1 / (Float)60;
                    newPos = transform.GlobalPosition + charBody.Velocity * dt;
                    b2Body_SetTransform(bodyId, ToB2(newPos), b2MakeRot((float)transform.GlobalRotation));
                }
            }

            transform.Position = newPos;
            charBody.RealVelocity = FromB2(vel);
        }
    }

    // ══════════════════════════════════════════════
    //  Box2D → ECS sync
    // ══════════════════════════════════════════════

    private static void SyncBox2DToEcs(EntityWorld state, Box2DPhysicsState physState)
    {
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>())
        {
            if (!physState.EntityToBody.TryGetValue(entity.Id, out var bodyId))
                continue;

            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var rb = ref state.GetComponent<RigidBody2D>(entity);

            var pos = b2Body_GetPosition(bodyId);
            var rot = b2Body_GetRotation(bodyId);
            var vel = b2Body_GetLinearVelocity(bodyId);

            transform.Position = FromB2(pos);
            transform.Rotation = (Float)b2Rot_GetAngle(rot);
            rb.LinearVelocity = FromB2(vel);
        }
    }

    // ══════════════════════════════════════════════
    //  Area2D sensor overlap processing
    // ══════════════════════════════════════════════

    private static void ProcessSensorEvents(EntityWorld state, Box2DPhysicsState physState)
    {
        // First: clear previous frame's entered/exited masks and remove exited entities
        foreach (var entity in state.Filter<Area2D>())
        {
            ref var area = ref state.GetComponent<Area2D>(entity);

            // Remove entities that exited last frame
            if (area.ExitedMask != 0)
            {
                for (int i = area.OverlappingEntities.Count - 1; i >= 0; i--)
                {
                    if ((area.ExitedMask & (1 << i)) != 0)
                        area.OverlappingEntities.RemoveAt(i);
                }
            }

            area.EnteredMask = 0;
            area.ExitedMask = 0;
        }

        // Read sensor events from Box2D
        var sensorEvents = b2World_GetSensorEvents(physState.WorldId);

        if (sensorEvents.beginCount > 0 || sensorEvents.endCount > 0)
            ILogger.Log($"[Box2D] Sensor events: {sensorEvents.beginCount} begin, {sensorEvents.endCount} end");

        // Process begin-touch events
        for (int i = 0; i < sensorEvents.beginCount; i++)
        {
            var evt = sensorEvents.beginEvents[i];
            B2BodyId sensorBodyId, visitorBodyId;
            try
            {
                sensorBodyId = b2Shape_GetBody(evt.sensorShapeId);
                visitorBodyId = b2Shape_GetBody(evt.visitorShapeId);
            }
            catch { continue; }

            if (!physState.BodyIndexToEntity.TryGetValue(sensorBodyId.index1, out int sensorEntityId))
                continue;
            if (!physState.BodyIndexToEntity.TryGetValue(visitorBodyId.index1, out int visitorEntityId))
                continue;

            var sensorEntity = new Entity(sensorEntityId);
            if (!state.HasComponent<Area2D>(sensorEntity)) continue;

            ref var area = ref state.GetComponent<Area2D>(sensorEntity);
            if (!area.Monitoring) continue;

            // Add to overlapping list if not already there
            bool found = false;
            for (int j = 0; j < area.OverlappingEntities.Count; j++)
            {
                if (area.OverlappingEntities[j] == visitorEntityId)
                {
                    found = true;
                    break;
                }
            }

            if (!found && area.OverlappingEntities.Count < 8)
            {
                int idx = area.OverlappingEntities.Count;
                area.OverlappingEntities.Add(visitorEntityId);
                area.EnteredMask |= (byte)(1 << idx);
            }
        }

        // Process end-touch events
        for (int i = 0; i < sensorEvents.endCount; i++)
        {
            var evt = sensorEvents.endEvents[i];

            // Shapes may have been destroyed (entity pruned) — guard with try-catch
            B2BodyId sensorBodyId, visitorBodyId;
            try
            {
                sensorBodyId = b2Shape_GetBody(evt.sensorShapeId);
                visitorBodyId = b2Shape_GetBody(evt.visitorShapeId);
            }
            catch { continue; }

            if (!physState.BodyIndexToEntity.TryGetValue(sensorBodyId.index1, out int sensorEntityId))
                continue;
            if (!physState.BodyIndexToEntity.TryGetValue(visitorBodyId.index1, out int visitorEntityId))
                continue;

            var sensorEntity = new Entity(sensorEntityId);
            if (!state.HasComponent<Area2D>(sensorEntity)) continue;

            ref var area = ref state.GetComponent<Area2D>(sensorEntity);

            for (int j = 0; j < area.OverlappingEntities.Count; j++)
            {
                if (area.OverlappingEntities[j] == visitorEntityId)
                {
                    area.ExitedMask |= (byte)(1 << j);
                    break;
                }
            }
        }

        // Log area state periodically
        var gt = state.GetCustomData<IGameTime>();
        if (gt != null && gt.CurrentTick % 300 == 0)
        {
            int areaCount = 0, withOverlaps = 0;
            foreach (var e in state.Filter<Area2D>())
            {
                areaCount++;
                var a = state.GetComponent<Area2D>(e);
                if (a.OverlappingEntities.Count > 0) withOverlaps++;
            }
            // Also check if Area2D entities have Box2D bodies
            int areasWithBodies = 0;
            foreach (var e in state.Filter<Area2D>())
            {
                if (physState.EntityToBody.ContainsKey(e.Id)) areasWithBodies++;
                else ILogger.Log($"[Box2D] Area2D entity {e.Id} has NO Box2D body!");
            }
            ILogger.Log($"[Box2D] Areas: {areaCount} total, {areasWithBodies} with bodies, {withOverlaps} with overlaps, bodyMap={physState.BodyIndexToEntity.Count}");
        }

        // Update HasOverlappingBodies flag
        foreach (var entity in state.Filter<Area2D>())
        {
            ref var area = ref state.GetComponent<Area2D>(entity);
            area.HasOverlappingBodies = area.OverlappingEntities.Count > 0;
        }
    }

    // ══════════════════════════════════════════════
    //  Conversion helpers
    // ══════════════════════════════════════════════

    private static B2Vec2 ToB2(Vector2 v) => new B2Vec2((float)v.X, (float)v.Y);
    private static Vector2 FromB2(B2Vec2 v) => new Vector2((Float)v.X, (Float)v.Y);
}

/// <summary>
/// Persistent Box2D physics state, stored as CustomData on EntityWorld.
/// Unlike RapierPhysicsState, this world is NOT rebuilt every frame —
/// Fixed64 determinism means it's safe to keep across ticks.
/// </summary>
public class Box2DPhysicsState : Deterministic.GameFramework.ECS.IDerivedState
{
    public B2WorldId WorldId;
    public Dictionary<int, B2BodyId> EntityToBody { get; } = new();
    public Dictionary<int, int> BodyIndexToEntity { get; } = new();

    /// <summary>
    /// Invalidates the Box2D world after rollback or state sync.
    /// Clears the entity↔body mapping caches. On the next Update(),
    /// SyncEcsToBox2D will find no mappings, prune all existing Box2D
    /// bodies (since none are "alive"), and re-create them from the
    /// restored ECS components. The world itself is kept — destroying
    /// and recreating it would lose internal solver state and produce
    /// non-deterministic results.
    /// </summary>
    public void Invalidate(bool full = false)
    {
        EntityToBody.Clear();
        BodyIndexToEntity.Clear();
        _pendingPrune = true;
    }

    /// <summary>Set by Invalidate(); forces a prune of all orphaned Box2D bodies.</summary>
    internal bool _pendingPrune;
}
