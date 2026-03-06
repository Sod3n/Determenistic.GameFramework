using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.CoreV2;
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

    // EntityId -> CharacterController
    private readonly Dictionary<int, RapierCharacterController> _entityToController = new();
    
    // Store serialized world states
    private readonly Dictionary<long, byte[]> _worldStateHistory = new();

    private const string ExternalStateKey = "RapierPhysics";

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
            StepCharacters(state);
            
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
        }

        // 4. Sync Physics to ECS
        SyncPhysicsToEcs(state);

        // 5. Save Physics State to History
        SaveWorldState(state, worldEntity);

        _lastSimulatedTick = state.GameLoop.CurrentTick;
    }
    
    private void StepCharacters(GlobalState state)
    {
        if (_world == null) return;
        var dt = (float)state.GameLoop.FixedDeltaTime;

        foreach (var entity in state.Filter<CharacterBody2D, Transform2D, CollisionShape2D>())
        {
            ref var character = ref state.GetComponent<CharacterBody2D>(entity);
            ref var transform = ref state.GetComponent<Transform2D>(entity);
            ref var shapeComp = ref state.GetComponent<CollisionShape2D>(entity);

            // Get or Create Controller
            if (!_entityToController.TryGetValue(entity.Id, out var controller))
            {
                controller = new RapierCharacterController(0.01f); // Default offset
                _entityToController[entity.Id] = controller;
            }

            // Update Controller Settings
            controller.SetUp(new RVector((float)character.UpDirection.X, (float)character.UpDirection.Y));
            controller.SetMaxSlopeClimbAngle((float)character.FloorMaxAngle);
            controller.SetMinSlopeSlideAngle((float)character.WallMinSlideAngle);
            controller.SetSlideEnabled(true);
            controller.SetSnapToGround((float)character.FloorSnapLength);
            // controller.SetAutostep(...) // TODO: Add if needed

            // Prepare Shape
            RapierShape? shape = null;
            if (shapeComp.Type == CollisionShapeType.Circle)
                shape = RapierShape.Ball((float)shapeComp.Circle.Radius);
            else if (shapeComp.Type == CollisionShapeType.Rectangle)
                shape = RapierShape.Cuboid((float)shapeComp.Rectangle.Size.X / 2.0f, (float)shapeComp.Rectangle.Size.Y / 2.0f);
            else if (shapeComp.Type == CollisionShapeType.Capsule)
                shape = RapierShape.Capsule((float)shapeComp.Capsule.Height / 2.0f, (float)shapeComp.Capsule.Radius);

            if (shape == null) continue;

            // Prepare Movement
            var shapePos = new RVector((float)transform.Position.X + (float)shapeComp.Position.X, (float)transform.Position.Y + (float)shapeComp.Position.Y);
            var shapeRot = new RRotation((float)transform.Rotation + (float)shapeComp.Rotation);
            
            // Desired translation based on Velocity * dt
            var desiredTranslation = new RVector((float)character.Velocity.X * dt, (float)character.Velocity.Y * dt);
            
            // Move
            // uint.MaxValue for all layers collision for now
            var result = controller.MoveShape(dt, _world, shape, shapePos, shapeRot, desiredTranslation, uint.MaxValue);
            
            // Update Transform
            var newPos = new Vector2(shapePos.x + result.translation.x - (float)shapeComp.Position.X, shapePos.y + result.translation.y - (float)shapeComp.Position.Y);
            transform.Position = newPos;
            
            // Update Character State
            character.IsOnFloor = result.grounded;
            // character.IsOnWall = ... // Need more info from result?
            // character.FloorNormal = ...
            
            // Calculate Real Velocity
            if (dt > 0)
            {
                character.RealVelocity = new Vector2(result.translation.x / dt, result.translation.y / dt);
            }
            else
            {
                character.RealVelocity = Vector2.Zero;
            }

            shape.Dispose();
        }
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
                }
            }
            
            foreach (var entity in state.Filter<StaticBody2D>())
            {
                var bodyComp = state.GetComponent<StaticBody2D>(entity);
                if (bodyComp.BodyId != 0)
                {
                    _entityToBody[entity.Id] = bodyComp.BodyId;
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
                _world?.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
                _world?.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);
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
    }

    private void CreateBodyForEntity(GlobalState state, Entity entity)
    {
        if (_world == null) return;

        ref var transform = ref state.GetComponent<Transform2D>(entity);
        
        ulong bodyHandle;
        
        if (state.HasComponent<RigidBody2D>(entity))
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            var translation = new RVector((float)transform.Position.X, (float)transform.Position.Y);
            var rotation = new RRotation((float)transform.Rotation); 

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
            var translation = new RVector((float)transform.Position.X, (float)transform.Position.Y);
            var rotation = new RRotation((float)transform.Rotation);

            bodyHandle = _world.BodyCreate(RRigidBodyType.Fixed, translation, rotation);
            body.BodyId = bodyHandle;
        }
        else
        {
            return;
        }

        _entityToBody[entity.Id] = bodyHandle;

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
                
                _world.ColliderCreate(shape, bodyHandle, colTranslation, colRotation, 0.5f, 0.0f); 
                shape.Dispose();
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
            transform.Position = new Vector2(rapierPos.x, rapierPos.y);
            transform.Rotation = rapierRot.angle;

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
    }
}
