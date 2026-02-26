namespace Deterministic.GameFramework.Network.NetworkState;

public struct NodeComponent : IComponent
{
    public Ref Owner;
    public Ref NextSibling;
    public Ref PrevSibling;
}