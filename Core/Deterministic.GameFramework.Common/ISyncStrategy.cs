namespace Deterministic.GameFramework.Common;

public interface ISyncStrategy
{
    void Attach(GameSimulation sim);
    void Detach(GameSimulation sim);
    bool PreSimulate(GameSimulation sim);
    void PostSimulate(GameSimulation sim);
    bool SuppressTickSnapshotBroadcast => false;
    bool SuppressActionBroadcast => false;
}
