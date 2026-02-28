using Deterministic.GameFramework.CoreV2.Extensions;

namespace Deterministic.GameFramework.CoreV2;

public struct Context
{
    public GlobalState State { get; }
    public Entity Entity { get; }

    public Context(GlobalState state, Entity entity)
    {
        State = state;
        Entity = entity;
    }
    
    public Entity CreateEntity<T>() where T : struct, IComponent
    {
        var entity = State.CreateEntity();
        entity.AddComponent(new T(), this);
        return entity;
    }
    
    public void Schedule<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        State.GameLoop.ScheduleOnTick(State.GameLoop.CurrentTick + 1, action, target);
    }
    
    
}
