using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public class SceneManager(GameSimulation gameSimulation)
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
        var systems = scene.RegisterSystems(gameSimulation);
        var systemList = systems.ToList();
        if (systemList.Count > 0)
        {
            Console.WriteLine($"[SceneManager] Registering {systemList.Count} systems...");
            _currentSceneSystems = gameSimulation.SystemRunner.EnableSystems(systemList);
        }

        // Register new actions/reactions
        var actionServices = scene.RegisterActionServices(gameSimulation);
        var reactionServices = scene.RegisterReactionServices(gameSimulation);
        
        _currentSceneActions = gameSimulation.Dispatcher.EnableActions(actionServices);
        _currentSceneReactions = gameSimulation.Dispatcher.EnableReactions(reactionServices);
        
        // Initialize scene content
        scene.OnEnter(gameSimulation);
    }

    public void UnloadCurrentScene()
    {
        if (_currentScene == null) return;

        _currentScene.OnExit(gameSimulation);
            
        _currentSceneSystems?.Dispose();
        _currentSceneSystems = null;

        _currentSceneActions?.Dispose();
        _currentSceneActions = null;
        
        _currentSceneReactions?.Dispose();
        _currentSceneReactions = null;
        
        // Destroy scene entities
        gameSimulation.State.ForEach((Entity e, ref SceneTag _) => gameSimulation.State.DeleteEntity(e));
        _currentScene = null;
    }
}
