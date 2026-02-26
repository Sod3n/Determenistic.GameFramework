using System;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;

namespace Deterministic.GameFramework.CoreV2.Example.Reactions;

[NetworkId(106)]
public class RegionDamageReaction : ReactionService<DamageAction, RegionDamageReactionTag>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true; // Run after the damage is applied

    protected override IsAborted React(DamageAction action, ref RegionDamageReactionTag tag, Context ctx)
    {
        // 'tag' is the component on 'ctx.Entity' (The Region)
        // We can now access other components on the Region easily via ctx.GetComponent<T>()
        // because ctx.Entity IS the Region.
        
        ref var region = ref ctx.GetComponent<RegionComponent>();
        region.DamageCounter += action.Amount;
        
        Console.WriteLine($"[Hierarchy Reaction] Region (Entity {ctx.Entity.Id}) detected damage of {action.Amount}. Total Region Damage: {region.DamageCounter}");
        return new IsAborted { Value = false };
    }
}
