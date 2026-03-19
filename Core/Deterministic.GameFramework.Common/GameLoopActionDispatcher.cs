using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public class GameLoopActionDispatcher(GameSimulation simulation) : IActionDispatcher
{
    public GameSimulation Simulation { get; } = simulation;
    
    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Simulation.ScheduleOnTick(Simulation.CurrentTick + 1, action, target);
    }
}