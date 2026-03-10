using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;

namespace Deterministic.GameFramework.CoreV2.Systems;

[UpdateOrder(-1000)] // Run before everything else (especially Physics)
public class TransformSystem : ISystem
{
    // ParentId -> List of ChildIds
    private readonly Dictionary<int, List<int>> _hierarchy = new(256);
    private readonly List<int> _roots = new(64);
    private readonly List<Entity> _entitiesToDestroy = new(16);

    public void Update(GlobalState state)
    {
        // 1. Build Hierarchy Tree (O(N))
        foreach (var list in _hierarchy.Values)
        {
            list.Clear();
        }
        _roots.Clear();
        _entitiesToDestroy.Clear();

        foreach (var entity in state.Filter<Transform2D>())
        {
            var transform = state.GetComponent<Transform2D>(entity);
            
            // Register in hierarchy if parent exists (regardless of whether parent will be destroyed this frame)
            // We need this to propagate destruction down.
            bool hasParent = transform.Parent != Entity.Null && state.HasComponent<Transform2D>(transform.Parent);
            
            if (hasParent)
            {
                if (!_hierarchy.TryGetValue(transform.Parent.Id, out var children))
                {
                    children = new List<int>(8);
                    _hierarchy[transform.Parent.Id] = children;
                }
                children.Add(entity.Id);
            }
            else
            {
                // Root or Orphan
                // If Orphan (parent was invalid/missing) and flag is set, mark for destroy
                if (transform.DestroyOnUnparent && transform.Parent != Entity.Null)
                {
                    _entitiesToDestroy.Add(entity);
                }
                else
                {
                    // Valid Root OR Orphan that survives
                    _roots.Add(entity.Id);
                }
            }
        }

        // 2. Propagate Destruction (Breadth-First via List appending)
        // We iterate by index because the list grows as we find children
        for (int i = 0; i < _entitiesToDestroy.Count; i++)
        {
            var parentEntity = _entitiesToDestroy[i];
            
            if (_hierarchy.TryGetValue(parentEntity.Id, out var children))
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
                            _entitiesToDestroy.Add(childEntity);
                        }
                        else
                        {
                            // Child survives but parent is dying -> Become Root
                            _roots.Add(childId);
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
        foreach (var entity in _entitiesToDestroy)
        {
            state.DeleteEntity(entity);
        }

        // 4. Update Transforms (DFS) (O(N))
        foreach (var rootId in _roots)
        {
            // Skip if somehow a root was added to destroy list (shouldn't happen with above logic)
            // Actually, we need to be careful not to process destroyed roots.
            // But _roots only contains survivors.
            UpdateTransformRecursive(state, rootId);
        }
    }

    private void UpdateTransformRecursive(GlobalState state, int entityId)
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
                float rad = -(float)parentTransform.GlobalRotation;
                float cos = MathF.Cos(rad);
                float sin = MathF.Sin(rad);
                
                var unrotatedPos = new Vector2(
                    relativePos.X * cos - relativePos.Y * sin,
                    relativePos.X * sin + relativePos.Y * cos
                );
                
                // 3. Unscale
                var pScale = parentTransform.GlobalScale;
                // Avoid divide by zero, though scale 0 is problematic generally
                Float sx = Float.Abs(pScale.X) > 0.00001f ? 1f / pScale.X : 0f;
                Float sy = Float.Abs(pScale.Y) > 0.00001f ? 1f / pScale.Y : 0f;
                
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
                float rad = (float)parentTransform.GlobalRotation;
                float cos = MathF.Cos(rad);
                float sin = MathF.Sin(rad);
                
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
        if (_hierarchy.TryGetValue(entityId, out var children))
        {
            foreach (var childId in children)
            {
                UpdateTransformRecursive(state, childId);
            }
        }
    }
}
