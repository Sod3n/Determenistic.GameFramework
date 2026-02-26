namespace Deterministic.GameFramework.Network.NetworkState;

public struct DieAction : IAction
{
    
}

[Deterministic.GameFramework.Network.NetworkId(442012472)]
public class DieActionHandler : ActionService<DieAction, HealthComponent>
{
    protected override void ExecuteProcess(DieAction args, ref HealthComponent target, Context context)
    {
        // TODO: Handle death
    }
}