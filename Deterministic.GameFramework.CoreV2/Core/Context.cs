using Deterministic.GameFramework.CoreV2.Extensions;

namespace Deterministic.GameFramework.CoreV2;

public struct Context : IActionDispatcher
{
    public GlobalState State { get; }
    public Entity Entity { get; }
    public IActionDispatcher Dispatcher { get; }

    public Context(GlobalState state, Entity entity, IActionDispatcher dispatcher)
    {
        State = state;
        Entity = entity;
        Dispatcher = dispatcher;
    }

    public Context(GlobalState state, Entity entity) : this(state, entity, state)
    {
    }
    
    public Entity CreateEntity<T>() where T : struct, IComponent
    {
        var entity = State.CreateEntity();
        entity.AddComponent(new T(), this);
        return entity;
    }
    
    public void Schedule<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Dispatcher.Dispatch(action, target);
    }
    
    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Dispatcher.Dispatch(action, target);
    }

    public void DestroyEntity(Entity entity)
    {
        State.DeleteEntity(entity);
    }
}
