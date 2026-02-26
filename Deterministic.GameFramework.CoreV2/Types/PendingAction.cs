namespace Deterministic.GameFramework.CoreV2;

public struct PendingAction
{
    public int NetworkId;
    public int TargetEntityId;
    public long ExecuteTick; // If this is for a future tick. For current tick, it equals CurrentTick.
    public int DataOffset;   // Where in the byte[] buffer this action's struct data begins
    public int DataLength;   // Size of the struct data in bytes
}
