using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;

namespace Deterministic.GameFramework.CoreV2.Example.Services;

[StableId("00000000-0000-0000-0000-000000000001")]
public class DamageActionHandler : ActionService<DamageAction, HealthComponent>
{
    protected override void ExecuteProcess(DamageAction args, ref HealthComponent target, Context ctx)
    {
        target.CurrentHealth.Value -= args.Amount.Value;
        if (target.CurrentHealth.Value > 0) return;
    }
}
