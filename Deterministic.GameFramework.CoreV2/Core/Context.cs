namespace Deterministic.GameFramework.CoreV2;

public struct Context
{
    public GlobalState State { get; }
    public Entity ExecutingEntity { get; }

    public Context(GlobalState state, Entity executingEntity)
    {
        State = state;
        ExecutingEntity = executingEntity;
    }

    public ref T GetComponent<T>() where T : struct, IComponent
    {
        return ref State.GetState<T>(ExecutingEntity);
    }
    
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        return ref State.GetState<T>(entity);
    }
}
