using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Extensions;

namespace Deterministic.GameFramework.CoreV2.Example.Reactions;

[StableId("00000000-0000-0000-0000-000000000106")]
public class RegionDamageReaction : ReactionService<DamageAction, HealthComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true; // Run after the damage is applied

    protected override bool ShouldReact(DamageAction action, HealthComponent health, Context ctx)
    {
        var tag = ctx.TryGetComponent<RegionDamageReactionTag>();
        if(tag == null) return false;
        
        bool hasParty = ctx.Entity.HasComponent(tag.Value.TargetParty, ctx);
        
        // Only react if the entity (e.g. Player) has a Party component matching the tag's target.
        return hasParty;
    }

    protected override IsAborted React(ref DamageAction action, ref HealthComponent health, Context ctx)
    {
        // 'tag' is the component on 'ctx.Entity' (The Player)
        // We update the RegionComponent on the Player itself.
        
        // Ensure entity has RegionComponent before accessing to avoid exceptions or create if needed
        if (ctx.State.HasComponent<RegionComponent>(ctx.Entity))
        {
            ref var region = ref ctx.State.GetComponent<RegionComponent>(ctx.Entity);
            region.DamageCounter += action.Amount;
        }
        
        return new IsAborted { Value = false };
    }
}
