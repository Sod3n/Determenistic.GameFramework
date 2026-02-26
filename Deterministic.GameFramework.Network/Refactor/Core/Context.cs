namespace Deterministic.GameFramework.Network.NetworkState;

public readonly ref struct Context(Entity entity, GlobalState world)
{
    public readonly Entity Entity = entity; // Immutable ID
    public readonly GlobalState World = world; // Access to other systems

    public ref T GetComponent<T>() where T : struct => ref World.GetState<T>(Entity);
    public Context GetParent() => throw new NotImplementedException();
}