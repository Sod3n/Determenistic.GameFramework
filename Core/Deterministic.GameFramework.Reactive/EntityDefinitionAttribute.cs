namespace Deterministic.GameFramework.Reactive;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false)]
public class EntityDefinitionAttribute : Attribute
{
    public Type[] Components { get; }

    public EntityDefinitionAttribute(params Type[] components)
    {
        Components = components;
    }
}
