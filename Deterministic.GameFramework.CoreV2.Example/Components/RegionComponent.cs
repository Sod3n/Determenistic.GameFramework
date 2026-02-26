using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Components;

[NetworkId(105)]
public struct RegionComponent : IComponent
{
    public int DamageCounter;
}
