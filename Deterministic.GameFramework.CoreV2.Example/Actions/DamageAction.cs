using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.CoreV2.Example.Actions;

public readonly struct DamageAction : IAction
{
    public Int Amount { get; }

    public DamageAction(int amount)
    {
        Amount = amount;
    }
}

public readonly struct DieAction : IAction
{
}
