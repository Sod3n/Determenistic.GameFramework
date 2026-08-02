using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

public interface IGameTime
{
    long CurrentTick { get; }
    Float FixedDeltaTime { get; }
    int TickRate { get; }
    bool IsResimulating { get; }
}
