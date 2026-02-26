namespace Deterministic.GameFramework.Network.NetworkState;

public struct HealthComponent : IComponent
{
    public Int CurrentHealth;
}

[Deterministic.GameFramework.Network.NetworkId(1192313640)]
public class DecreaseDamageReaction : ReactionService<DamageAction, HealthComponent>
{
    public override int Priority => PriorityDefault;
    public override bool AfterActionExecuted => true;
    
    protected override IsAborted React(DamageAction args, ref HealthComponent target, Context context)
    {
        return new IsAborted { Value = false };
    }
} 
