using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Components;

[NetworkId("00000000-0000-0000-0000-000000000105")]
public struct RegionComponent : IComponent
{
    public int DamageCounter;
}
