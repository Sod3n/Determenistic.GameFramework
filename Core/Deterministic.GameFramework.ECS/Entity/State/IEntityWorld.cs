using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

public delegate void ComponentAction1<T1>(ref T1 c1);
public delegate void ComponentAction2<T1, T2>(ref T1 c1, ref T2 c2);
public delegate void ComponentActionEntity1<T1>(Entity e, ref T1 c1);
public delegate void ComponentActionEntity2<T1, T2>(Entity e, ref T1 c1, ref T2 c2);
public delegate void ComponentActionEntity2<T1, T2, TState>(TState state, Entity e, ref T1 c1, ref T2 c2);

public interface IEntityWorld
{
    int NextEntityId { get; }
    BitMask128[] EntityMasks { get; }
    Dictionary<string, byte[]> ExternalState { get; set; }

    Entity CreateEntity();
    Entity CreateEntity<T>() where T : struct, IComponent;
    void ResetComponents(bool clearCache = false);
    
    ref T AddComponent<T>(Entity entity, T component) where T : struct, IComponent;
    void RemoveComponent<T>(Entity entity) where T : struct, IComponent;
    void DeleteEntity(Entity entity);
    
    ref T GetComponent<T>(Entity entity) where T : struct, IComponent;
    T? TryGetComponent<T>(Entity entity) where T : struct, IComponent;
    bool HasComponent<T>(Entity entity) where T : struct, IComponent;
    
    IEnumerable<Entity> Filter<T>() where T : struct, IComponent;
    IEnumerable<Entity> Filter<T1, T2>() where T1 : struct, IComponent where T2 : struct, IComponent;
    IEnumerable<Entity> Filter<T1, T2, T3>() where T1 : struct, IComponent where T2 : struct, IComponent where T3 : struct, IComponent;
    
    void ForEach<T1>(ComponentAction1<T1> action) where T1 : struct, IComponent;
    void ForEach<T1, T2>(ComponentAction2<T1, T2> action) where T1 : struct, IComponent where T2 : struct, IComponent;
    void ForEach<T1>(ComponentActionEntity1<T1> action) where T1 : struct, IComponent;
    void ForEach<T1, T2>(ComponentActionEntity2<T1, T2> action) where T1 : struct, IComponent where T2 : struct, IComponent;
    void ForEach<T1, T2, TState>(TState state, ComponentActionEntity2<T1, T2, TState> action) where T1 : struct, IComponent where T2 : struct, IComponent;
    
    void RegisterComponent<T>() where T : struct, IComponent;
    AlignedComponentStore<T> GetComponentStore<T>() where T : struct, IComponent;
    
    void ClearDirty();
    IReadOnlyList<int> GetDirtyEntities();
}
