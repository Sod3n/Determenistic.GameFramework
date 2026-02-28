using System;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Extensions;

namespace Deterministic.GameFramework.CoreV2.Example.Reactions;

[NetworkId(106)]
public class RegionDamageReaction : ReactionService<DamageAction, RegionComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true; // Run after the damage is applied

    protected override bool ShouldReact(DamageAction action, RegionComponent reg, Context ctx)
    {
        var tag = ctx.TryGetComponent<RegionDamageReactionTag>();
        if(tag == null) return false;
        // Debug logging
        bool hasParty = ctx.Entity.HasComponent(tag.Value.TargetParty, ctx);
        Console.WriteLine($"[RegionDamageReaction] ShouldReact Entity={ctx.Entity.Id} HasParty={hasParty} TargetPartyId={tag.Value.TargetParty.PartyId}");
        
        // Only react if the entity (e.g. Player) has a Party component matching the tag's target.
        // This demonstrates filtering: The reaction runs locally on the entity that has the Tag.
        return hasParty;
    }

    protected override IsAborted React(ref DamageAction action, ref RegionComponent region, Context ctx)
    {
        // 'tag' is the component on 'ctx.Entity' (The Player)
        // We update the RegionComponent on the Player itself.
        region.DamageCounter += action.Amount;
        Console.WriteLine($"[RegionDamageReaction] Entity {ctx.Entity.Id} processed damage {action.Amount}. Region Counter: {region.DamageCounter}");
        
        return new IsAborted { Value = false };
    }
}
