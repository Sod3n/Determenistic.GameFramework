using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;

namespace Deterministic.GameFramework.CoreV2.Example.Services;

[NetworkId(1)]
public class DamageActionHandler : ActionService<DamageAction, HealthComponent>
{
    protected override void ExecuteProcess(DamageAction args, ref HealthComponent target, Context ctx)
    {
        target.CurrentHealth.Value -= args.Amount.Value;
        if (target.CurrentHealth.Value > 0) return;
        
        // Find parent using tree
        if (ctx.State.HasComponent<HierarchyComponent>(ctx.Entity))
        {
            var tree = ctx.GetComponent<HierarchyComponent>();
            if (tree.ParentId != 0) // 0 means no parent in this simple PoC
            {
                var parent = new Entity(tree.ParentId);
                // Dispatch DieAction to parent (assuming parent has HealthComponent, or just a marker)
                // In real code we'd need the dispatcher in Context to run nested actions or schedule them
            }
        }
    }
}
