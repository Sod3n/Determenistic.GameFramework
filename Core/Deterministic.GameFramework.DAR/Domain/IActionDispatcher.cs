using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR;

public interface IActionDispatcher
{
    void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction;
}
