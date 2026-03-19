using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;

namespace Deterministic.GameFramework.CoreV2.Example.Reactions;

[StableId("00000000-0000-0000-0000-000000000002")]
public class DecreaseDamageReaction : ReactionService<DamageAction, HealthComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true;
    
    protected override IsAborted React(ref DamageAction args, ref HealthComponent target, Context context)
    {
        // Simple reaction: after taking damage, do something (PoC)
        return new IsAborted { Value = false };
    }
}
