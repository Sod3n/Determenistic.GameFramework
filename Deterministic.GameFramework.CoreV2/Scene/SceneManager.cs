using System;
using System.Collections.Generic;
using System.Linq;

namespace Deterministic.GameFramework.CoreV2.Scene;

public class SceneManager
{
    private readonly GameLoop _gameLoop;
    private IScene? _currentScene;
    private readonly List<ISystem> _currentSceneSystems = new();

    public IScene? CurrentScene => _currentScene;

    public SceneManager(GameLoop gameLoop)
    {
        _gameLoop = gameLoop;
    }

    public void LoadScene(IScene scene)
    {
        Console.WriteLine($"[SceneManager] Loading Scene: {scene.GetType().Name}");

        // 1. Unload previous scene
        if (_currentScene != null)
        {
            Console.WriteLine($"[SceneManager] Unloading: {_currentScene.GetType().Name}");
            _currentScene.OnExit(_gameLoop);
            
            // Remove scene-specific systems
            if (_currentSceneSystems.Count > 0)
            {
                Console.WriteLine($"[SceneManager] Removing {_currentSceneSystems.Count} systems...");
                _gameLoop.RemoveSystems(_currentSceneSystems);
                
                // Dispose systems if they implement IDisposable
                foreach (var system in _currentSceneSystems)
                {
                    if (system is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SceneManager] Error disposing system {system.GetType().Name}: {ex}");
                        }
                    }
                }
                
                _currentSceneSystems.Clear();
            }

            // Destroy scene entities
            var entitiesToDestroy = _gameLoop.State.Filter<SceneTag>().ToList();
            Console.WriteLine($"[SceneManager] Destroying {entitiesToDestroy.Count} scene entities...");
            foreach (var entity in entitiesToDestroy)
            {
                _gameLoop.State.DeleteEntity(entity);
            }
        }

        // 2. Load new scene
        _currentScene = scene;
        
        // Register new systems
        var systems = scene.RegisterSystems(_gameLoop);
        if (systems != null)
        {
            var systemList = systems.ToList();
            if (systemList.Count > 0)
            {
                Console.WriteLine($"[SceneManager] Registering {systemList.Count} systems...");
                _gameLoop.RegisterSystems(systemList);
                _currentSceneSystems.AddRange(systemList);
            }
        }

        // Initialize scene content
        scene.OnEnter(_gameLoop);
        
        Console.WriteLine($"[SceneManager] Scene Loaded: {scene.GetType().Name}");
    }
}
