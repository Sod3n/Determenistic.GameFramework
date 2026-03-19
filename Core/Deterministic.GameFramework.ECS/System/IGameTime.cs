namespace Deterministic.GameFramework.ECS;

public interface IGameTime
{
    long CurrentTick { get; }
    float FixedDeltaTime { get; }
    int TickRate { get; }
    bool IsResimulating { get; }
}
