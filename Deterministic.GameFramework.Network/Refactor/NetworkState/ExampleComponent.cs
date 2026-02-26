namespace Deterministic.GameFramework.Network.NetworkState;
using System;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

public struct ExampleComponent : IComponent
{
    public Int CurrentTick;
    public Int TickRate;
}


[Deterministic.GameFramework.Network.NetworkId(874855072)]
public class TickProvider : Reaction
{
    public TickProvider(LeafDomain target) : base(target)
    {
    }
}
