using System;
using System.Collections.Generic;
using System.Linq;

namespace Deterministic.GameFramework.CoreV2.Scene;

public class SceneManager(GameLoop gameLoop)
{
    private IScene? _currentScene;
    private IDisposable? _currentSceneSystems;
    private IDisposable? _currentSceneActions;
    private IDisposable? _currentSceneReactions;

    public IScene? CurrentScene => _currentScene;

    public void LoadScene(IScene scene)
    {
        UnloadCurrentScene();
        
        _currentScene = scene;
        
        // Register new systems
        var systems = scene.RegisterSystems(gameLoop);
        var systemList = systems.ToList();
        if (systemList.Count > 0)
        {
            Console.WriteLine($"[SceneManager] Registering {systemList.Count} systems...");
            _currentSceneSystems = gameLoop.SystemRunner.EnableSystems(systemList);
        }

        // Register new actions/reactions
        var actionServices = scene.RegisterActionServices(gameLoop);
        var reactionServices = scene.RegisterReactionServices(gameLoop);
        
        _currentSceneActions = gameLoop.Dispatcher.EnableActions(actionServices);
        _currentSceneReactions = gameLoop.Dispatcher.EnableReactions(reactionServices);
        
        // Initialize scene content
        scene.OnEnter(gameLoop);
    }

    public void UnloadCurrentScene()
    {
        if (_currentScene == null) return;

        _currentScene.OnExit(gameLoop);
            
        _currentSceneSystems?.Dispose();
        _currentSceneSystems = null;

        _currentSceneActions?.Dispose();
        _currentSceneActions = null;
        
        _currentSceneReactions?.Dispose();
        _currentSceneReactions = null;
        
        // Destroy scene entities
        gameLoop.State.ForEach((Entity e, ref SceneTag _) => gameLoop.State.DeleteEntity(e));
        _currentScene = null;
    }
}
