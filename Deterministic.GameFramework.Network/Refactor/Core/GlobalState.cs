namespace Deterministic.GameFramework.Network.NetworkState;

public class GlobalState
{
    private Array[] _componentArrays = new Array[128];
    
    public ref T GetState<T>(Entity entity) where T : struct {
        var typeId = InternalTypeId<T>.Value;
        
        if (typeId >= _componentArrays.Length) {
            Array.Resize(ref _componentArrays, _componentArrays.Length * 2);
        }

        var specificArray = (T[])_componentArrays[typeId];
        return ref specificArray[entity.Id];
    }
    
    public void Execute<TAction>(TAction action, Entity entity)
        where TAction : struct, IAction 
    {
        var service = GetServiceFor<TAction>();
        service.Execute(action, this, entity);
    }
}

public static class TypeCounter
{

    private static int _nextTypeId = 0;
    internal static int Next() => _nextTypeId++;
}

internal static class InternalTypeId<T> {
    public static readonly int Value = TypeCounter.Next(); 
}