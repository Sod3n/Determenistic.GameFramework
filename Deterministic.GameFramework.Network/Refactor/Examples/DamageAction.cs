using Deterministic.GameFramework.Network.Refactor.Extensions;

namespace Deterministic.GameFramework.Network.NetworkState;

public readonly struct NetworkAction<TAction>
{
    public Int ServiceId { get; }
    public Int TargetId { get; }
    public TAction Action { get; }
}

public readonly struct DamageAction : IAction
{
    public Int Amount { get; }
}

[Deterministic.GameFramework.Network.NetworkId(1185411312)]
public class DamageActionHandler : ActionService<DamageAction, HealthComponent>
{
    protected override void ExecuteProcess(DamageAction args, ref HealthComponent target, Context ctx)
    {
        target.CurrentHealth.Value -= args.Amount.Value;
        if (target.CurrentHealth.Value > 0) return;
        
        var parent = ctx.GetParent();
        var parentHealth = parent.GetComponent<HealthComponent>();
        new DieAction().Execute(parent);
    }
}

