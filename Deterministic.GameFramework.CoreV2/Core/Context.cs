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

    public ref T GetComponent<T>() where T : struct, IComponent
    {
        return ref State.GetState<T>(Entity);
    }
    
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        return ref State.GetState<T>(entity);
    }
}
