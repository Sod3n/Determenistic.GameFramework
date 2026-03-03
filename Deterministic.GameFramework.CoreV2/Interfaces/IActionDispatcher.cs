namespace Deterministic.GameFramework.CoreV2;

public interface IActionDispatcher
{
    void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction;
}
