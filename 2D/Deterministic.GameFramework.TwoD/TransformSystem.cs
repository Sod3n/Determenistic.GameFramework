using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.TwoD;

[UpdateOrder(-1000)] // Run before everything else (especially Physics)
public class TransformSystem : ISystem
{
    // Stateless system (buffers are local to Update to support multi-threading/multiple matches)
    public void Update(EntityWorld state)
    {
        // 1. Build Hierarchy Tree (O(N))
        var hierarchy = new Dictionary<int, List<int>>(256);
        var roots = new List<int>(64);
        var entitiesToDestroy = new List<Entity>(16);

        foreach (var entity in state.Filter<Transform2D>())
        {
            var transform = state.GetComponent<Transform2D>(entity);
            
            // Register in hierarchy if parent exists (regardless of whether parent will be destroyed this frame)
            // We need this to propagate destruction down.
            bool hasParent = transform.Parent != Entity.Null && state.HasComponent<Transform2D>(transform.Parent);
            
            if (hasParent)
            {
                if (!hierarchy.TryGetValue(transform.Parent.Id, out var children))
                {
                    children = new List<int>(8);
                    hierarchy[transform.Parent.Id] = children;
                }
                children.Add(entity.Id);
            }
            else
            {
                // Root or Orphan
                // If Orphan (parent was invalid/missing)
                if (transform.Parent != Entity.Null)
                {
                    if (transform.DestroyOnUnparent)
                    {
                        entitiesToDestroy.Add(entity);
                    }
                    else
                    {
                        // Valid Orphan that survives -> Becomes Root
                        roots.Add(entity.Id);
                        
                        // We must clear the parent pointer to reflect root status
                        transform.Parent = Entity.Null;
                        state.AddComponent(entity, transform);
                    }
                }
                else
                {
                    // Valid Root
                    roots.Add(entity.Id);
                }
            }
        }

        // 2. Propagate Destruction (Breadth-First via List appending)
        // We iterate by index because the list grows as we find children
        for (int i = 0; i < entitiesToDestroy.Count; i++)
        {
            var parentEntity = entitiesToDestroy[i];
            
            if (hierarchy.TryGetValue(parentEntity.Id, out var children))
            {
                foreach (var childId in children)
                {
                    var childEntity = new Entity(childId);
                    // Child might have been deleted? No, filter ensured it exists.
                    // We need to check if child should be destroyed or reparented (to root)
                    if (state.TryGetComponent<Transform2D>(childEntity) is { } childTransform)
                    {
                        if (childTransform.DestroyOnUnparent)
                        {
                            entitiesToDestroy.Add(childEntity);
                        }
                        else
                        {
                            // Child survives but parent is dying -> Become Root
                            roots.Add(childId);
                            // We also need to clear its parent pointer potentially? 
                            // TransformSystem updates absolute position anyway, 
                            // but for data consistency we might want to set Parent = Null.
                            // However, we can't easily modify component here while iterating?
                            // Actually we can, we have the ID.
                            childTransform.Parent = Entity.Null;
                            state.AddComponent(childEntity, childTransform); // Write back
                        }
                    }
                }
            }
        }

        // 3. Process deferred deletions
        foreach (var entity in entitiesToDestroy)
        {
            state.DeleteEntity(entity);
        }

        // 4. Update Transforms (DFS) (O(N))
        foreach (var rootId in roots)
        {
            // Skip if somehow a root was added to destroy list (shouldn't happen with above logic)
            // Actually, we need to be careful not to process destroyed roots.
            // But _roots only contains survivors.
            UpdateTransformRecursive(state, rootId, hierarchy);
        }
    }

    private void UpdateTransformRecursive(EntityWorld state, int entityId, Dictionary<int, List<int>> hierarchy)
    {
        // 1. Update Self
        var entity = new Entity(entityId);
        ref var transform = ref state.GetComponent<Transform2D>(entity);

        bool parentExists = transform.Parent != Entity.Null && state.HasComponent<Transform2D>(transform.Parent);

        // Check for Manual Global Change (Global -> Local)
        bool globalChanged = 
            transform.GlobalPosition != transform.LastGlobalPosition ||
            transform.GlobalRotation != transform.LastGlobalRotation || 
            transform.GlobalScale != transform.LastGlobalScale;

        if (globalChanged)
        {
            // Global -> Local
            if (!parentExists)
            {
                transform.Position = transform.GlobalPosition;
                transform.Rotation = transform.GlobalRotation;
                transform.Scale = transform.GlobalScale;
            }
            else
            {
                // Calculate Local from Global and Parent
                ref var parentTransform = ref state.GetComponent<Transform2D>(transform.Parent);
                
                // 1. Untranslate
                var relativePos = transform.GlobalPosition - parentTransform.GlobalPosition;
                
                // 2. Unrotate
                Float rad = -parentTransform.GlobalRotation;
                Float cos = Float.Cos(rad);
                Float sin = Float.Sin(rad);
                
                var unrotatedPos = new Vector2(
                    relativePos.X * cos - relativePos.Y * sin,
                    relativePos.X * sin + relativePos.Y * cos
                );
                
                // 3. Unscale
                var pScale = parentTransform.GlobalScale;
                // Avoid divide by zero, though scale 0 is problematic generally
                Float sx = Float.Abs(pScale.X) > 0.00001f ? 1 / pScale.X : 0;
                Float sy = Float.Abs(pScale.Y) > 0.00001f ? 1 / pScale.Y : 0;
                
                transform.Position = unrotatedPos * new Vector2(sx, sy);
                
                // Rotation
                transform.Rotation = transform.GlobalRotation - parentTransform.GlobalRotation;
                
                // Scale (Simplified for 2D non-skewed)
                transform.Scale = transform.GlobalScale * new Vector2(sx, sy);
            }
        }
        else
        {
            // Local -> Global (Standard)
            if (!parentExists)
            {
                // Root: World = Local
                transform.GlobalPosition = transform.Position;
                transform.GlobalRotation = transform.Rotation;
                transform.GlobalScale = transform.Scale;
            }
            else
            {
                // Child: World = Parent * Local
                ref var parentTransform = ref state.GetComponent<Transform2D>(transform.Parent);
                
                // 1. Scale Local Position
                var scaledLocalPos = transform.Position * parentTransform.GlobalScale;
                
                // 2. Rotate Local Position
                Float rad = parentTransform.GlobalRotation;
                Float cos = Float.Cos(rad);
                Float sin = Float.Sin(rad);
                
                var rotatedLocalPos = new Vector2(
                    scaledLocalPos.X * cos - scaledLocalPos.Y * sin,
                    scaledLocalPos.X * sin + scaledLocalPos.Y * cos
                );
                
                // 3. Translate
                transform.GlobalPosition = parentTransform.GlobalPosition + rotatedLocalPos;
                
                // 4. Rotate & Scale
                transform.GlobalRotation = parentTransform.GlobalRotation + transform.Rotation;
                transform.GlobalScale = parentTransform.GlobalScale * transform.Scale;
            }
        }
        
        // Update Change Tracking
        transform.LastGlobalPosition = transform.GlobalPosition;
        transform.LastGlobalRotation = transform.GlobalRotation;
        transform.LastGlobalScale = transform.GlobalScale;

        // 2. Process Children
        if (hierarchy.TryGetValue(entityId, out var children))
        {
            foreach (var childId in children)
            {
                UpdateTransformRecursive(state, childId, hierarchy);
            }
        }
    }
}
