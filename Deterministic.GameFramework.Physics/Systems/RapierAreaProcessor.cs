using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.Physics.Components;
using uniffi.rapier_uniffi;

namespace Deterministic.GameFramework.Physics.Systems;

internal class RapierAreaProcessor
{
    public void UpdateAreaOverlaps(GlobalState state, RapierWorld world, Dictionary<ulong, int> bodyToEntity)
    {
        if (world == null) return;

        foreach (var entity in state.Filter<Area2D, CollisionShape2D, Transform2D>())
        {
            ref var area = ref state.GetComponent<Area2D>(entity);
            if (!area.Monitoring)
            {
                area.HasOverlappingBodies = false;
                area.OverlappingEntities.Clear();
                continue;
            }

            ref var shapeComp = ref state.GetComponent<CollisionShape2D>(entity);
            ref var transform = ref state.GetComponent<Transform2D>(entity);

            // Reconstruct shape for query
            RapierShape? shape = null;
            if (shapeComp.Type == CollisionShapeType.Circle)
                shape = RapierShape.Ball((float)shapeComp.Circle.Radius);
            else if (shapeComp.Type == CollisionShapeType.Rectangle)
                shape = RapierShape.Cuboid((float)shapeComp.Rectangle.Size.X / 2.0f, (float)shapeComp.Rectangle.Size.Y / 2.0f);
            else if (shapeComp.Type == CollisionShapeType.Capsule)
                shape = RapierShape.Capsule((float)shapeComp.Capsule.Height / 2.0f, (float)shapeComp.Capsule.Radius);

            if (shape == null) continue;

            var shapePos = new RVector(
                (float)transform.GlobalPosition.X + (float)shapeComp.Position.X, 
                (float)transform.GlobalPosition.Y + (float)shapeComp.Position.Y
            );
            var shapeRot = new RRotation((float)transform.GlobalRotation + (float)shapeComp.Rotation);

            // Query world for overlaps
            var intersections = world.IntersectShape(shape, shapePos, shapeRot, area.CollisionMask);
            
            // Capture previous state
            var prevOverlaps = area.OverlappingEntities;
            var prevExitedMask = area.ExitedMask;
            
            area.OverlappingEntities.Clear();
            area.EnteredMask = 0;
            area.ExitedMask = 0;
            
            // 1. Process Current Intersections (Active)
            // We use a temporary way to track what we've added to avoid duplicates if multiple colliders map to same entity
            // Since List8 is small, we can just linear scan
            
            foreach (var colliderHandle in intersections)
            {
                ulong bodyHandle = world.ColliderGetParent(colliderHandle);
                
                if (bodyToEntity.TryGetValue(bodyHandle, out int targetId))
                {
                    if (targetId == entity.Id) continue; // Ignore self

                    // Check if already added this frame (multiple colliders on same entity)
                    bool alreadyAdded = false;
                    for (int i = 0; i < area.OverlappingEntities.Count; i++)
                    {
                        if (area.OverlappingEntities[i] == targetId)
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                    if (alreadyAdded) continue;

                    if (area.OverlappingEntities.Count < List8<int>.Capacity)
                    {
                        area.OverlappingEntities.Add(targetId);
                        int newIndex = area.OverlappingEntities.Count - 1;
                        
                        // Check if it was present in previous frame
                        bool wasPresent = false;
                        for (int k = 0; k < prevOverlaps.Count; k++)
                        {
                            // If it was in prev list AND NOT marked as exited last frame
                            // (If it was marked exited last frame, it means it left and came back, so it is "Entered")
                            if (prevOverlaps[k] == targetId && (prevExitedMask & (1 << k)) == 0)
                            {
                                wasPresent = true;
                                break;
                            }
                        }

                        if (!wasPresent)
                        {
                            area.EnteredMask |= (byte)(1 << newIndex);
                        }
                    }
                }
            }

            // 2. Process Exited Entities
            // Look for entities that were in prevOverlaps but NOT in current OverlappingEntities
            // AND were not already processed as "Exited" in the previous frame (those are now gone)
            
            for (int k = 0; k < prevOverlaps.Count; k++)
            {
                // If it was already marked as exited last frame, we drop it now (it's gone)
                if ((prevExitedMask & (1 << k)) != 0) continue;

                int prevId = prevOverlaps[k];
                
                // Check if it is still active (in current list)
                bool isStillActive = false;
                for (int i = 0; i < area.OverlappingEntities.Count; i++)
                {
                    if (area.OverlappingEntities[i] == prevId)
                    {
                        isStillActive = true;
                        break;
                    }
                }

                if (!isStillActive)
                {
                    // It has exited this frame. Add it to list and mark Exited.
                    if (area.OverlappingEntities.Count < List8<int>.Capacity)
                    {
                        area.OverlappingEntities.Add(prevId);
                        int newIndex = area.OverlappingEntities.Count - 1;
                        area.ExitedMask |= (byte)(1 << newIndex);
                    }
                }
            }

            area.HasOverlappingBodies = area.OverlappingEntities.Count > 0;

            shape.Dispose();
        }
    }
}
