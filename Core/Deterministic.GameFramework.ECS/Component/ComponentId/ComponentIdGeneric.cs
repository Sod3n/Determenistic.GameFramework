
namespace Deterministic.GameFramework.ECS;

/// <summary>
/// High-performance static cache for component lookups.
/// Replaces InternalTypeId<T>.
/// </summary>
public static class ComponentId<T> where T : struct, IComponent
{
    // Lazy initialization ensures this runs AFTER RegisterAssembly is called, 
    // assuming the first access happens inside the game loop/factory.
    public static ComponentId Id = ComponentId.FromType<T>();

    public static DenseComponentId DenseId = Id.ToDense();
    public static StableComponentId StableId = Id.ToStable();
    
    // Fast integer access for array indexing in ECS
    public static int IntId = Id.ToDense().Value;

    static ComponentId()
    {
        ComponentId.RegisterCacheUpdate(typeof(T), (stable, dense) =>
        {
            var componentId = ComponentId.FromStable(stable); // Re-create with new mapping
            
            Id = componentId;
            DenseId = dense;
            StableId = stable;
            IntId = dense.Value;
        });
    }
}
