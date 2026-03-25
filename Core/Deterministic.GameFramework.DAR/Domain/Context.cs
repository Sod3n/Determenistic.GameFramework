
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.DAR;

public readonly struct Context(EntityWorld state, Entity entity, IActionDispatcher dispatcher) : IEntityWorld, IActionDispatcher
{
    public EntityWorld State { get; } = state;
    public Entity Entity { get; } = entity;
    public IActionDispatcher Dispatcher { get; } = dispatcher;
    
    
    // IActionDispatcher Interface
    
    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction => Dispatcher.Dispatch(action, target);

    // IEntityWorld Interface
    
    public int NextEntityId  => State.NextEntityId;
    public BitMask128[] EntityMasks => State.EntityMasks;
    public Dictionary<string, byte[]> ExternalState {get => State.ExternalState; set => State.ExternalState = value;}
    public Entity CreateEntity() => State.CreateEntity();
    public Entity CreateEntity<T>() where T : struct, IComponent => State.CreateEntity<T>();
    public void ResetComponents(bool clearCache = false) => State.ResetComponents(clearCache);
    ref T IEntityWorld.AddComponent<T>(Entity entity, T component) => ref State.AddComponent<T>(entity, component);
    public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent => State.AddComponent(entity, component);
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent => State.RemoveComponent<T>(entity);
    public void DeleteEntity(Entity entity) => State.DeleteEntity(entity);
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent => ref State.GetComponent<T>(entity);
    public T? TryGetComponent<T>(Entity entity) where T : struct, IComponent => State.TryGetComponent<T>(entity);
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent => State.HasComponent<T>(entity);
    public IEnumerable<Entity> Filter<T>() where T : struct, IComponent => State.Filter<T>();
    public IEnumerable<Entity> Filter<T1, T2>() where T1 : struct, IComponent where T2 : struct, IComponent => State.Filter<T1, T2>();
    public IEnumerable<Entity> Filter<T1, T2, T3>() where T1 : struct, IComponent where T2 : struct, IComponent where T3 : struct, IComponent => State.Filter<T1, T2, T3>();
    public void ForEach<T1>(ComponentAction1<T1> action) where T1 : struct, IComponent => State.ForEach(action);
    public void ForEach<T1, T2>(ComponentAction2<T1, T2> action) where T1 : struct, IComponent where T2 : struct, IComponent => State.ForEach(action);
    public void ForEach<T1>(ComponentActionEntity1<T1> action) where T1 : struct, IComponent => State.ForEach(action);
    public void ForEach<T1, T2>(ComponentActionEntity2<T1, T2> action) where T1 : struct, IComponent where T2 : struct, IComponent => State.ForEach(action);
    public void ForEach<T1, T2, TState>(TState state, ComponentActionEntity2<T1, T2, TState> action) where T1 : struct, IComponent where T2 : struct, IComponent => State.ForEach(state, action);
    public void RegisterComponent<T>() where T : struct, IComponent => State.RegisterComponent<T>();
    public AlignedComponentStore<T> GetComponentStore<T>() where T : struct, IComponent => State.GetComponentStore<T>();
    public void ClearDirty() => State.ClearDirty();
    public IReadOnlyList<int> GetDirtyEntities() => State.GetDirtyEntities();
}
