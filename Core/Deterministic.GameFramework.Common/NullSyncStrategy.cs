namespace Deterministic.GameFramework.Common;

public class NullSyncStrategy : ISyncStrategy
{
    public void Attach(GameSimulation sim) { }
    public void Detach(GameSimulation sim) { }
    public bool PreSimulate(GameSimulation sim) => true;
    public void PostSimulate(GameSimulation sim) { }
}
