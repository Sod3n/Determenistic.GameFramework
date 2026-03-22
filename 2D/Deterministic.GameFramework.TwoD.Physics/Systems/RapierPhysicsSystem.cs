using System;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics2D.Systems;

public class RapierPhysicsSystem : ISystem, IDisposable
{
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
    }

    public void Update(EntityWorld state)
    {
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        if (physicsState == null)
        {
            physicsState = new RapierPhysicsState();
            state.SetCustomData(physicsState);
            
            // Initialize world
            physicsState.World = new RapierWorld();
        }

        var gameTime = state.GetCustomData<IGameTime>();
        if (gameTime == null)
        {
             // Fallback or throw? For now return to avoid crash, but system won't work.
             return; 
        }

        var worldEntity = GetWorldEntity(state);

        // 1. Detect Rollback / Initialization / Jump Forward (Network Sync)
        // If we are not strictly proceeding to the next tick, we must restore/reset.
        if (physicsState.World == null || gameTime.CurrentTick != physicsState.LastSimulatedTick + 1)
        {
            ResetOrRestoreWorld(state, physicsState, worldEntity);
        }

        // 1.5 Prune Bodies (Remove destroyed entities)
        PruneBodies(state, physicsState);

        // 2. Sync ECS changes to Physics (Creation/Destruction)
        SyncEcsToPhysics(state, physicsState);

        // 3. Step Physics
        if (physicsState.World != null)
        {
            var dt = (float)gameTime.FixedDeltaTime;
            
            // Step Characters (Kinematic Movement) before physics step
            physicsState.CharacterProcessor.StepCharacters(state, physicsState.World, physicsState.EntityToBody, dt);
            
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
            
            physicsState.World.Step(gravity, integrationParams);
            
            // 3.5 Update Area2D Overlaps
            physicsState.AreaProcessor.UpdateAreaOverlaps(state, physicsState.World, physicsState.BodyToEntity);
        }

        // 4. Sync Physics to ECS
        SyncPhysicsToEcs(state, physicsState);

        // 5. Save Physics State to History
        SaveWorldState(state, physicsState, worldEntity, gameTime);

        physicsState.LastSimulatedTick = gameTime.CurrentTick;
    }
    
    private Entity GetWorldEntity(EntityWorld state)
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

    private void ResetOrRestoreWorld(EntityWorld state, RapierPhysicsState physicsState, Entity worldEntity)
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

        if (state.ExternalState.TryGetValue(ExternalStateKey, out var snapshotData))
        {
            try
            {
                physicsState.World = RapierWorld.Deserialize(snapshotData);
                // We still need to map ECS to Rapier bodies, but since the handles are preserved
                // in the ECS components, we can reconstruct the EntityToBody mappings
                RebuildMappingsFromECS(state, physicsState);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Rapier] Failed to deserialize physics state, falling back to rebuild: {ex}");
            }
        }

        physicsState.World = new RapierWorld();
        // Rebuild from ECS
        RebuildWorldFromECS(state, physicsState);
    }
    
    private void RebuildMappingsFromECS(EntityWorld state, RapierPhysicsState physicsState)
    {
        foreach (var entity in state.Filter<RigidBody2D>())
            AddMapping(physicsState, entity.Id, state.GetComponent<RigidBody2D>(entity).BodyId);
            
        foreach (var entity in state.Filter<StaticBody2D>())
            AddMapping(physicsState, entity.Id, state.GetComponent<StaticBody2D>(entity).BodyId);
            
        foreach (var entity in state.Filter<CharacterBody2D>())
            AddMapping(physicsState, entity.Id, state.GetComponent<CharacterBody2D>(entity).BodyId);
            
        // Because of the duplicate issue with Area2D vs CharacterBody2D on Cow, 
        // we map it if the entity doesn't already have a mapping.
        foreach (var entity in state.Filter<Area2D>())
        {
            if (!physicsState.EntityToBody.ContainsKey(entity.Id))
            {
                AddMapping(physicsState, entity.Id, state.GetComponent<Area2D>(entity).BodyId);
            }
        }
    }
    
    private void AddMapping(RapierPhysicsState physicsState, int entityId, ulong bodyHandle)
    {
        if (bodyHandle == ulong.MaxValue) return; // Uninitialized
        physicsState.EntityToBody[entityId] = bodyHandle;
        physicsState.BodyToEntity[bodyHandle] = entityId;
    }
    
    private void RebuildWorldFromECS(EntityWorld state, RapierPhysicsState physicsState)
    {
        // Rebuild RigidBodies
        var rigidBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) rigidBodies.Add(entity);
        rigidBodies.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in rigidBodies)
        {
            ref var body = ref state.GetComponent<RigidBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }
        
        // Rebuild StaticBodies
        var staticBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) staticBodies.Add(entity);
        staticBodies.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in staticBodies)
        {
            ref var body = ref state.GetComponent<StaticBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }

        // Rebuild CharacterBodies
        var charBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) charBodies.Add(entity);
        charBodies.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in charBodies)
        {
            ref var body = ref state.GetComponent<CharacterBody2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }

        // Rebuild Area2Ds
        var areaBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) areaBodies.Add(entity);
        areaBodies.Sort((a, b) => a.Id.CompareTo(b.Id));
        foreach (var entity in areaBodies)
        {
            ref var body = ref state.GetComponent<Area2D>(entity);
            body.BodyId = 0;
            CreateBodyForEntity(state, physicsState, entity);
        }
    }
    
    private void PruneBodies(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        var entitiesToRemove = new System.Collections.Generic.List<int>();

        // SORT FOR DETERMINISM: Dictionary iteration is undefined
        var sortedEntityIds = new List<int>(physicsState.EntityToBody.Keys);
        sortedEntityIds.Sort();

        foreach (var entityId in sortedEntityIds)
        {
            var entity = new Entity(entityId);

            // Check if entity is valid and has relevant components
            // If entity is deleted, HasComponent will return false for everything
            bool isValid = state.HasComponent<Transform2D>(entity) &&
                           (state.HasComponent<RigidBody2D>(entity) ||
                            state.HasComponent<StaticBody2D>(entity) ||
                            state.HasComponent<CharacterBody2D>(entity) ||
                            state.HasComponent<Area2D>(entity));

            if (!isValid)
            {
                entitiesToRemove.Add(entityId);
            }
        }

        foreach (var entityId in entitiesToRemove)
        {
            ulong bodyHandle = physicsState.EntityToBody[entityId];
            
            physicsState.World.BodyDestroy(bodyHandle);
            
            physicsState.EntityToBody.Remove(entityId);
            physicsState.BodyToEntity.Remove(bodyHandle);
            
            // Clean up character controller if it exists
            physicsState.CharacterProcessor.RemoveCharacter(entityId);
        }
    }

    private void SyncEcsToPhysics(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        // 1. Dynamic Bodies
        var rigidBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<RigidBody2D, Transform2D>()) rigidBodies.Add(entity);
        rigidBodies.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in rigidBodies)
        {
            if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
            {
                CreateBodyForEntity(state, physicsState, entity);
            }
            else
            {
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                ref var transform = ref state.GetComponent<Transform2D>(entity);

                // Check for Teleport (Logic moved Transform)
                // FORCE SYNC for determinism: Always snap Physics to ECS state at start of frame.
                // This ensures that any floating point drift in Rapier is corrected by the quantized ECS state.
                // It effectively forces Rapier to restart from the canonical FixedPoint state every tick.
                
                physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
                
                physicsState.World.BodySetLinvel(bodyHandle, new RVector((float)body.LinearVelocity.X, (float)body.LinearVelocity.Y), true);
                physicsState.World.BodySetAngvel(bodyHandle, new RVector((float)body.AngularVelocity, 0), true);
            }
        }
        
        // 2. Static Bodies
        var staticBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<StaticBody2D, Transform2D>()) staticBodies.Add(entity);
        staticBodies.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in staticBodies)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             // Static bodies usually don't move, but if logic moves them, we must sync.
             else 
             {
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }
        
        // 3. Character Bodies
        var charBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<CharacterBody2D, Transform2D>()) charBodies.Add(entity);
        charBodies.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in charBodies)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 // Character Bodies are Kinematic, so we control them.
                 // Sync Logic position to Physics body
                 ref var transform = ref state.GetComponent<Transform2D>(entity);
                 
                 physicsState.World.BodySetTranslation(bodyHandle, new RVector((float)transform.GlobalPosition.X, (float)transform.GlobalPosition.Y), true);
                 physicsState.World.BodySetRotation(bodyHandle, new RRotation((float)transform.GlobalRotation), true);
             }
        }

        // 4. Area2D
        var areaBodies = new System.Collections.Generic.List<Entity>();
        foreach (var entity in state.Filter<Area2D, Transform2D>()) areaBodies.Add(entity);
        areaBodies.Sort((a, b) => a.Id.CompareTo(b.Id));

        foreach (var entity in areaBodies)
        {
             if (!physicsState.EntityToBody.TryGetValue(entity.Id, out ulong bodyHandle))
             {
                 CreateBodyForEntity(state, physicsState, entity);
             }
             else
             {
                 // Sync position for Area2D (Kinematic)
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

            // Area2D is Kinematic so we can move it freely, but it doesn't have mass/forces
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
                
                ulong colliderHandle = physicsState.World.ColliderCreate(shape, bodyHandle, colTranslation, colRotation, 0.5f, 0.0f); 
                shape.Dispose();

                // Special handling for Area2D
                if (state.HasComponent<Area2D>(entity))
                {
                    ref var area = ref state.GetComponent<Area2D>(entity);
                    physicsState.World.ColliderSetSensor(colliderHandle, true);
                    
                    // Set Collision Groups (Layer/Mask)
                    // Rapier uses (memberships, filter)
                    // We map Layer -> Memberships, Mask -> Filter
                    physicsState.World.ColliderSetCollisionGroups(colliderHandle, area.CollisionLayer, area.CollisionMask);
                }
                else
                {
                     // Apply collision groups from RigidBody2D or CharacterBody2D if present
                     uint layer = 0xFFFF0001; // Default
                     uint mask = 0xFFFF0001;  // Default

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
                         // Static bodies usually default layer unless we add fields to StaticBody2D too.
                         // For now, keep default or assume everything collides with static.
                     }

                     physicsState.World.ColliderSetCollisionGroups(colliderHandle, layer, mask);
                }
            }
        }
    }

    private void SyncPhysicsToEcs(EntityWorld state, RapierPhysicsState physicsState)
    {
        if (physicsState.World == null) return;

        // Iterate known bodies and update ECS
        // SORT FOR DETERMINISM: Dictionary iteration is undefined, so we must sort by ID.
        var sortedEntityIds = new List<int>(physicsState.EntityToBody.Keys);
        sortedEntityIds.Sort();

        foreach (var entityId in sortedEntityIds)
        {
            ulong bodyHandle = physicsState.EntityToBody[entityId];
            var entity = new Entity(entityId);

            if (!state.HasComponent<Transform2D>(entity)) continue;

            // ONLY update Transform from Physics for Dynamic Bodies (RigidBody2D).
            // Kinematic (CharacterBody2D, Area2D) and Static bodies are driven by logic/ECS.
            // Overwriting them with Rapier's float position introduces quantization error/drift.
            if (state.HasComponent<RigidBody2D>(entity))
            {
                // Get Position/Rotation
                var rapierPos = physicsState.World.BodyGetTranslation(bodyHandle);
                var rapierRot = physicsState.World.BodyGetRotation(bodyHandle);
                
                ref var transform = ref state.GetComponent<Transform2D>(entity);
                transform.GlobalPosition = new Vector2(rapierPos.x, rapierPos.y);
                transform.GlobalRotation = rapierRot.angle;

                // Get Velocity
                var rapierVel = physicsState.World.BodyGetLinvel(bodyHandle);
                var rapierAngVel = physicsState.World.BodyGetAngvel(bodyHandle);
                
                ref var body = ref state.GetComponent<RigidBody2D>(entity);
                body.LinearVelocity = new Vector2(rapierVel.x, rapierVel.y);
                body.AngularVelocity = rapierAngVel.x;
            }
        }
    }
    
    private void SaveWorldState(EntityWorld state, RapierPhysicsState physicsState, Entity worldEntity, IGameTime gameTime)
    {
        if (physicsState.World != null)
        {
            var currentTick = gameTime.CurrentTick;
            
            // Serialize
            // Save every tick for rollback support
            {
                var data = physicsState.World.Serialize();
                physicsState.WorldStateHistory[currentTick] = data;
                
                // Store in ECS component for purely ECS-based rollback systems (optional)
                if (state.HasComponent<PhysicsWorldState>(worldEntity))
                {
                    ref var physicsStateComp = ref state.GetComponent<PhysicsWorldState>(worldEntity);
                    physicsStateComp.Tick = currentTick;
                }
                
                state.ExternalState[ExternalStateKey] = data;
            }
        }
        
        // Prune history
        long oldestTick = gameTime.CurrentTick - 300; // 5 seconds
        var keysToRemove = new List<long>();
        foreach (var key in physicsState.WorldStateHistory.Keys)
        {
            if (key < oldestTick) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            physicsState.WorldStateHistory.Remove(key);
        }
    }

    public void Dispose()
    {
        // State is managed by EntityWorld.SystemData and cleaned up via ClearCustomData()
    }
}
