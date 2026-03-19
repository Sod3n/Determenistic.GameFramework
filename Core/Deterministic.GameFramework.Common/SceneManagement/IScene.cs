using System.Collections.Generic;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public interface IScene
{
    /// <summary>
    /// Register systems specific to this scene.
    /// These systems will be automatically unregistered when the scene unloads.
    /// </summary>
    IEnumerable<ISystem> RegisterSystems(GameSimulation loop);
    IEnumerable<IActionService> RegisterActionServices(GameSimulation loop);
    IEnumerable<IReactionService> RegisterReactionServices(GameSimulation loop);
    
    /// <summary>
    /// Called when the scene is entered. Use this to spawn initial entities (Map, Spawners, UI).
    /// </summary>
    void OnEnter(GameSimulation loop);
    
    /// <summary>
    /// Called when the scene is exited. Use this for custom cleanup if needed.
    /// Entities with SceneTag are automatically destroyed by SceneManager.
    /// </summary>
    void OnExit(GameSimulation loop);
}
