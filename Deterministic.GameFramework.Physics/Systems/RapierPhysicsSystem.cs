using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.Physics.Components;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics.Systems;

public class RapierPhysicsSystem : ISystem, IDisposable
{
    private RapierWorld? _world;
    private long _lastSimulatedTick = -1;
    
    // Keep track of which entities have bodies in the current world
    // EntityId -> BodyHandle
    private readonly Dictionary<int, ulong> _entityToBody = new();
    // BodyHandle -> EntityId (Reverse lookup for events)
    private readonly Dictionary<ulong, int> _bodyToEntity = new();

    // Processors
    private readonly RapierCharacterProcessor _characterProcessor = new();
    private readonly RapierAreaProcessor _areaProcessor = new();
    
    // Store serialized world states
    private readonly Dictionary<long, byte[]> _worldStateHistory = new();

    private const string ExternalStateKey = "RapierPhysics";

    static RapierPhysicsSystem()
    {
        RapierNativeLoader.Initialize();
    }

#if NETCOREAPP3_0_OR_GREATER
#endif

    public RapierPhysicsSystem()
    {
        // Initialize the native library if needed
        // GodotRapierMethods.InitPhysics(); // Not needed with uniffi
        _world = new RapierWorld();
    }

    public void Update(GlobalState state)
    {
        var worldEntity = GetWorldEntity(state);

        // 1. Detect Rollback / Initialization / Jump Forward (Network Sync)
        // If we are not strictly proceeding to the next tick, we must restore/reset.
        if (_world == null || state.GameLoop.CurrentTick != _lastSimulatedTick + 1)
        {
            ResetOrRestoreWorld(state, worldEntity);
        }

        // 2. Sync ECS changes to Physics (Creation/Destruction)
        SyncEcsToPhysics(state);

        // 3. Step Physics
        if (_world != null)
        {
            // Step Characters (Kinematic Movement) before physics step
            _characterProcessor.StepCharacters(state, _world, _entityToBody);
            
            var gravity = new RVector(0.0f, 0.0f);
            var dt = (float)state.GameLoop.FixedDeltaTime;
            
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
            
            _world.Step(gravity, integrationParams);
            
            // 3.5 Update Area2D Overlaps
            _areaProcessor.UpdateAreaOverlaps(state, _world, _bodyToEntity);
        }

        // 4. Sync Physics to ECS
        SyncPhysicsToEcs(state);

        // 5. Save Physics State to History
        SaveWorldState(state, worldEntity);

        _lastSimulatedTick = state.GameLoop.CurrentTick;
    }
    
    private Entity GetWorldEntity(GlobalState state)
    {
        // Assumption: There is exactly one entity with the World component.
        // Optimized: GlobalState creates it at startup, usually ID 0.
        // We can cache this if needed, but finding it via Filter is safe.
        foreach (var e in state.Filter<World>())
        {
            return e;
        }
        // Fallback if not found (shouldn't happen in standard usage)
        return new Entity(0);
    }

    private void ResetOrRestoreWorld(GlobalState state, Entity worldEntity)
    {
        // Dispose existing world
        if (_world != null)
        {
            _world.Dispose();
            _world = null;
            _entityToBody.Clear();
            _bodyToEntity.Clear();
            _characterProcessor.Clear();
        }

        bool restored = false;

        // Try to restore from PhysicsWorldState
        if (state.HasComponent<PhysicsWorldState>(worldEntity))
        {
            var physicsState = state.GetComponent<PhysicsWorldState>(worldEntity);
            
            byte[]? data = null;

            if (_worldStateHistory.TryGetValue(physicsState.Tick, out var historyData))
            {
                data = historyData;
            }
            else if (state.ExternalState.TryGetValue(ExternalStateKey, out var externalData))
            {
                data = externalData;
                _worldStateHistory[physicsState.Tick] = externalData;
            }

            if (data != null && data.Length > 0)
            {
                try 
                {
                    _world = RapierWorld.Deserialize(data);
                    restored = true;
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[RapierPhysicsSystem] Failed to deserialize world: {e.Message}");
                }
            }
        }

        if (!restored)
        {
            _world = new RapierWorld();
            // Rebuild from ECS if we couldn't restore (or if it's a fresh start)
            RebuildWorldFromECS(state);
        }
        else
        {
            // If restored, we need to rebuild the entity map from the ECS components
            // which should match the restored physics state.
            foreach (var entity in state.Filter<RigidBody2D>())
            {
                var bodyComp = state.GetComponent<RigidBody2D>(entity);
                if (bodyComp.BodyId != 0)
                {
                    _entityToBody[entity.Id] = bodyComp.BodyId;
                    _bodyToEntity[bodyComp.BodyId] = entity.Id;
                }
            }
            
            foreach (var entity in state.Filter<StaticBody2D>())
            {
                var bodyComp = state.GetComponent<StaticBody2D>(entity);
                if (bodyComp.BodyId != 0)
                {
                    _entityToBody[entity.Id] = bodyComp.BodyId;
                    _bodyToEntity[bodyComp.BodyId] = entity.Id;
                }
            }
            
            foreach (var entity in state.Filter<CharacterBody2D>())
            {
                var bodyComp = state.GetComponent<CharacterBody2D>(entity);
                if (bodyComp.BodyId != 0)
                {
                    _entityToBody[entity.Id] = bodyComp.BodyId;
                    _bodyToEntity[bodyComp.BodyId] = entity.Id;
                }
            }

            foreach (var entity in state.Filter<Area2D>())
            {
                var bodyComp = state.GetComponent<Area2D>(entity);
                if (bodyComp.BodyId != 0)
                {
                    _entityToBody[entity.Id] = bodyComp.BodyId;
                    _bodyToEntity[bodyComp.BodyId] = entity.Id;
                }
            }
        }
    }
    
    private void RebuildWorldFromECS(GlobalState state)
    {
        // Rebuild RigidBodies
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>())
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, entity);
        }
        
        // Rebuild StaticBodies
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>())
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, entity);
        }

        // Rebuild CharacterBodies
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>())
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, entity);
        }

        // Rebuild Area2Ds
        foreach (var entity in state.Filter<Area2D, Transform2D>())
        {
            ref var body = ref state.GetComponent<Area2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, entity);
        }
    }

    private void SaveWorldState(GlobalState state, Entity worldEntity)
    {
        if (_world == null) return;

        var data = _world.Serialize();
        long currentTick = state.GameLoop.CurrentTick;

        _worldStateHistory[currentTick] = data;
        state.ExternalState[ExternalStateKey] = data;

        if (!state.HasComponent<PhysicsWorldState>(worldEntity))
        {
            state.AddComponent(worldEntity, new PhysicsWorldState { Tick = currentTick });
        }
        else
        {
            ref var storage = ref state.GetComponent<PhysicsWorldState>(worldEntity);
            storage.Tick = currentTick;
        }
    }

    private void SyncEcsToPhysics(GlobalState state)
    {
        if (_world == null) return;

        // 1. Dynamic Bodies
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>())
        {
            if (!_entityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
            {
                CreateBodyForEntity(state, entity);
            }
            else
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                ref var transform = ref state.GetComponent<Transform2D>(entity);

                // Check for Teleport (Logic moved Transform)
                // If ECS position differs from Physics position, we assume logic moved it.
                var rapierPos = _world.BodyGetTranslation(bodyHandle);
                float dx = (float)transform.GlobalPosition.X - rapierPos.x;
                float dy = (float)transform.GlobalPosition.Y - rapierPos.y;
                
                if (dx * dx + dy * dy > 0.0001f) // Epsilon squared (0.01 * 0.01 = 0.0001)
                {
                    _world.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                    _world.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
                }

                _world.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
                _world.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);
            }
        }
        
        // 2. Static Bodies
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>())
        {
             if (!_entityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, entity);
             }
        }
        
        // 3. Character Bodies
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>())
        {
             if (!_entityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, entity);
             }
             else
             {
                 // Check for Teleport
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 var rapierPos = _world.BodyGetTranslation(bodyHandle);
                 float dx = (float)transform.GlobalPosition.X - rapierPos.x;
                 float dy = (float)transform.GlobalPosition.Y - rapierPos.y;
                 
                 if (dx * dx + dy * dy > 0.0001f)
                 {
                     _world.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                     // Characters usually handle rotation differently (upright), but we sync it anyway if changed
                     _world.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
                 }
             }
        }

        // 4. Area2D
        foreach (var entity in state.Filter<Area2D, Transform2D>())
        {
             if (!_entityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, entity);
             }
             else
             {
                 // Sync position for Area2D (Kinematic)
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 _world?.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 _world?.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }
    }

    private void CreateBodyForEntity(GlobalState state, Entity entity)
    {
        if (_world == null) return;

        ref var transform = ref state.GetComponent<Transform2D>(entity);
        
        ulong bodyHandle;
        
        if (state.HasComponent<RigidBody2D>(entity))
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation); 

            bodyHandle = _world.BodyCreate(RRigidBodyType.Dynamic, translation, rotation);
            
            _world.BodySetMass(bodyHandle, (float)body.Mass, true);
            _world.BodySetGravityScale(bodyHandle, (float)body.GravityScale, true);
            _world.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
            _world.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);
            
            _world.BodySetLinearDamping(bodyHandle, (float)body.LinearDamping);
            _world.BodySetAngularDamping(bodyHandle, (float)body.AngularDamping);
            _world.BodySetCcdEnabled(bodyHandle, body.CcdEnabled);
            
            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<StaticBody2D>(entity))
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = _world.BodyCreate(RRigidBodyType.Fixed, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<CharacterBody2D>(entity))
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            bodyHandle = _world.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else if (state.HasComponent<Area2D>(entity))
        {
            ref var body = ref state.GetComponent<Area2D>(entity);
            var translation = new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y);
            var rotation = new RRotation((float)transform.GlobalRotation);

            // Area2D is Kinematic so we can move it freely, but it doesn't have mass/forces
            bodyHandle = _world.BodyCreate(RRigidBodyType.KinematicPositionBased, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else
        {
            return;
        }

        _entityToBody[entity.Id] = bodyHandle;
        _bodyToEntity[bodyHandle] = entity.Id;

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
                // Cuboid takes half-extents
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
                
                ulong colliderHandle = _world.ColliderCreate(shape, bodyHandle, colTranslation, colRotation, 0.5f, 0.0f); 
                shape.Dispose();

                // Special handling for Area2D
                if (state.HasComponent<Area2D>(entity))
                {
                    ref var area = ref state.GetComponent<Area2D>(entity);
                    _world.ColliderSetSensor(colliderHandle, true);
                    
                    // Set Collision Groups (Layer/Mask)
                    // Rapier uses (memberships, filter)
                    // We map Layer -> Memberships, Mask -> Filter
                    _world.ColliderSetCollisionGroups(colliderHandle, area.CollisionLayer, area.CollisionMask);
                }
                else
                {
                     // For regular bodies, we might want to expose Layer/Mask on RigidBody2D too later.
                     // Defaulting to All for now or using default 0xFFFF0001 if we had a component for it.
                }
            }
        }
    }

    private void SyncPhysicsToEcs(GlobalState state)
    {
        if (_world == null) return;

        // Iterate known bodies and update ECS
        foreach (var kvp in _entityToBody)
        {
            int entityId = kvp.Key;
            ulong bodyHandle = kvp.Value;
            var entity = new Entity(entityId);

            if (!state.HasComponent<Transform2D>(entity)) continue;

            // Get Position/Rotation
            var rapierPos = _world.BodyGetTranslation(bodyHandle);
            var rapierRot = _world.BodyGetRotation(bodyHandle); // Returns RRotation
            
            ref var transform = ref state.GetComponent<Transform2D>(entity);
            transform.GlobalPosition = new Vector2(rapierPos.x, rapierPos.y);
            transform.GlobalRotation = rapierRot.angle;

            // Sync World -> Local if root
            // If it has a parent, we would need to calculate local from world (Inverse Transform),
            // but usually physics bodies are roots or independent.
            if (transform.Parent == Entity.Null)
            {
                transform.Position = transform.GlobalPosition;
                transform.Rotation = transform.GlobalRotation;
            }

            // Get Velocity for Dynamic Bodies
            if (state.HasComponent<RigidBody2D>(entity))
            {
                var rapierVel = _world.BodyGetLinvel(bodyHandle);
                var rapierAngVel = _world.BodyGetAngvel(bodyHandle);
                
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                body.LinearVelocity = new Vector2(rapierVel.x, rapierVel.y);
                body.AngularVelocity = rapierAngVel.x;
            }
        }
    }

    public void Dispose()
    {
        if (_world != null)
        {
            _world.Dispose();
            _world = null;
        }
        _characterProcessor.Clear();
    }
}
