using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Components;

[NetworkId(100)]
public struct HealthComponent : IComponent
{
    public Int CurrentHealth;
}
