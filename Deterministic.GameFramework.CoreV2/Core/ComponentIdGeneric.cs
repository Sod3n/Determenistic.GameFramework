using ComponentIdStruct = global::Deterministic.GameFramework.CoreV2.ComponentId;

namespace Deterministic.GameFramework.CoreV2;

/// <summary>
/// High-performance static cache for component lookups.
/// Replaces InternalTypeId<T>.
/// </summary>
public static class ComponentId<T> where T : struct, IComponent
{
    // Lazy initialization ensures this runs AFTER RegisterAssembly is called, 
    // assuming the first access happens inside the game loop/factory.
    public static ComponentIdStruct Id = ComponentIdStruct.FromType<T>();

    public static DenseComponentId DenseId = Id.ToDense();
    public static StableComponentId StableId = Id.ToStable();
    
    // Fast integer access for array indexing in ECS
    public static int IntId = Id.ToDense().Value;

    static ComponentId()
    {
        ComponentIdStruct.RegisterCacheUpdate(typeof(T), (stable, dense) =>
        {
            var componentId = ComponentIdStruct.FromStable(stable); // Re-create with new mapping
            
            Id = componentId;
            DenseId = dense;
            StableId = stable;
            IntId = dense.Value;
        });
    }
}
