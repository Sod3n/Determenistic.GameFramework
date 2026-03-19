using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Actions;

[StableId("10000000-0000-0000-0000-000000000001")]
public readonly struct DamageAction : IAction
{
    public Int Amount { get; }

    public DamageAction(int amount)
    {
        Amount = amount;
    }
}

[StableId("10000000-0000-0000-0000-000000000002")]
public readonly struct DieAction : IAction
{
}
