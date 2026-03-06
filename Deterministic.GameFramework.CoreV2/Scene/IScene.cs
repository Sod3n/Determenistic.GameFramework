using System.Collections.Generic;

namespace Deterministic.GameFramework.CoreV2.Scene;

public interface IScene
{
    /// <summary>
    /// Register systems specific to this scene.
    /// These systems will be automatically unregistered when the scene unloads.
    /// </summary>
    IEnumerable<ISystem> RegisterSystems(GameLoop loop);
    
    /// <summary>
    /// Called when the scene is entered. Use this to spawn initial entities (Map, Spawners, UI).
    /// </summary>
    void OnEnter(GameLoop loop);
    
    /// <summary>
    /// Called when the scene is exited. Use this for custom cleanup if needed.
    /// Entities with SceneTag are automatically destroyed by SceneManager.
    /// </summary>
    void OnExit(GameLoop loop);
}
