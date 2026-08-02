using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Scenes;

public class SceneManager(GameSimulation gameSimulation)
{
    private IScene? _currentScene;
    private IDisposable? _currentSceneSystems;
    private IDisposable? _currentSceneActions;

    public IScene? CurrentScene => _currentScene;

    public void LoadScene(IScene scene)
    {
        UnloadCurrentScene();

        _currentScene = scene;

        var systems = scene.RegisterSystems(gameSimulation);
        var systemList = systems.ToList();
        if (systemList.Count > 0)
        {
            ILogger.Log($"[SceneManager] Registering {systemList.Count} systems...");
            _currentSceneSystems = gameSimulation.SystemRunner.EnableSystems(systemList);
        }

        var actionServices = scene.RegisterActionServices(gameSimulation);
        _currentSceneActions = gameSimulation.Dispatcher.EnableActions(actionServices);

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

        gameSimulation.State.ForEach((Entity e, ref SceneTag _) => gameSimulation.State.DeleteEntity(e));
        _currentScene = null;
    }
}
