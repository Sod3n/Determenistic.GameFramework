using System.Collections.Generic;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Scenes;

public interface IScene
{
    IEnumerable<ISystem> RegisterSystems(GameSimulation loop);
    IEnumerable<IActionService> RegisterActionServices(GameSimulation loop);
    void OnEnter(GameSimulation loop);
    void OnExit(GameSimulation loop);
}
