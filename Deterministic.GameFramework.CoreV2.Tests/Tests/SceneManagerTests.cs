using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Scene;
using Xunit;
using FluentAssertions;

namespace Deterministic.GameFramework.CoreV2.Tests.Scene
{
    public class SceneManagerTests : IDisposable
    {
        private readonly GameLoop _loop;
        private readonly SceneManager _sceneManager;
        private readonly GlobalState _state;
        private readonly Dispatcher _dispatcher;
        private readonly ActionScheduler _scheduler;

        public SceneManagerTests()
        {
            ServiceLocator.RegisterAssembly(typeof(SceneManagerTests).Assembly);
            ServiceLocator.RegisterAssembly(typeof(World).Assembly);
            _state = new GlobalState();
            _dispatcher = new Dispatcher();
            _scheduler = new ActionScheduler();
            _loop = new GameLoop(_state, _dispatcher, _scheduler);
            _sceneManager = new SceneManager(_loop);
        }

        public void Dispose()
        {
            _loop.Dispose();
        }

        [Fact]
        public void LoadScene_ShouldCallOnEnter()
        {
            var mockScene = new MockScene();
            _sceneManager.LoadScene(mockScene);

            mockScene.OnEnterCalled.Should().BeTrue("OnEnter should be called when loading a scene.");
            mockScene.LoopPassedToEnter.Should().Be(_loop, "GameLoop should be passed to OnEnter.");
        }

        [Fact]
        public void LoadScene_ShouldCallOnExit_WhenReplacingScene()
        {
            var scene1 = new MockScene();
            var scene2 = new MockScene();

            _sceneManager.LoadScene(scene1);
            scene1.OnEnterCalled.Should().BeTrue();
            scene1.OnExitCalled.Should().BeFalse();

            _sceneManager.LoadScene(scene2);
            
            scene1.OnExitCalled.Should().BeTrue("OnExit should be called on the old scene.");
            scene2.OnEnterCalled.Should().BeTrue("OnEnter should be called on the new scene.");
        }

        [Fact]
        public void UnloadCurrentScene_ShouldCallOnExit_AndClearCurrentScene()
        {
            var scene = new MockScene();
            _sceneManager.LoadScene(scene);
            
            _sceneManager.UnloadCurrentScene();

            scene.OnExitCalled.Should().BeTrue("OnExit should be called when unloading.");
            _sceneManager.CurrentScene.Should().BeNull("CurrentScene should be null after unload.");
        }
        
        [Fact]
        public void LoadScene_ShouldClearSceneTaggedEntities()
        {
            var scene1 = new MockScene();
            _sceneManager.LoadScene(scene1);

            // Create an entity tagged with SceneTag
            var entity = _state.CreateEntity();
            _state.AddComponent(entity, new SceneTag());
            
            // Create a persistent entity (no SceneTag)
            var persistentEntity = _state.CreateEntity();
            _state.AddComponent(persistentEntity, new TestComponent());
            
            // Load new scene
            var scene2 = new MockScene();
            _sceneManager.LoadScene(scene2);

            // Assert
            // Entity with SceneTag should be deleted (mask cleared)
            _state.HasComponent<SceneTag>(entity).Should().BeFalse("Entity with SceneTag should be destroyed/cleared.");
            
            // Persistent Entity should still have its component
            _state.HasComponent<TestComponent>(persistentEntity).Should().BeTrue("Entity without SceneTag should remain.");
        }

        [Fact]
        public void LoadScene_ShouldRegisterAndUnregisterSystems()
        {
            var system = new MockSystem();
            var mockScene = new MockScene(new[] { system });

            // Load
            _sceneManager.LoadScene(mockScene);
            
            // Verify system is registered (mock system increments counter on Update)
            _loop.RunSingleTick();
            system.UpdateCount.Should().Be(1, "System should be registered and updated.");

            // Unload
            _sceneManager.UnloadCurrentScene();

            // Verify system is unregistered
            _loop.RunSingleTick();
            system.UpdateCount.Should().Be(1, "System should be unregistered and not updated anymore.");
        }

        [Fact]
        public void LoadScene_WithServiceLocator_ShouldRegisterServices()
        {
            var scene = new MockServiceLocatorScene();
            
            // Load Scene (Triggers ServiceLocator.Register)
            _sceneManager.LoadScene(scene);

            // Verify System is registered
            MockAutoRegSystem.UpdateCount = 0;
            _loop.RunSingleTick();
            MockAutoRegSystem.UpdateCount.Should().Be(1, "ServiceLocator registered system should run.");

            // Verify Action Service is registered
            // We can check if Dispatcher has the dense ID mapping
            var actionType = typeof(MockAction);
            int denseId = _dispatcher.GetDenseId<MockAction>();
            _dispatcher.GetActionType(denseId).Should().Be(actionType);
            
            // Unload Scene (Triggers ServiceLocator.Unregister)
            _sceneManager.UnloadCurrentScene();

            // Verify System is unregistered
            MockAutoRegSystem.UpdateCount = 0;
            _loop.RunSingleTick();
            MockAutoRegSystem.UpdateCount.Should().Be(0, "ServiceLocator unregistered system should not run.");

            // Verify Action Service is unregistered
            Action getAction = () => _dispatcher.GetDenseId<MockAction>();
            getAction.Should().Throw<Exception>("Action should be unregistered from Dispatcher.");
        }

        [Fact]
        public void ServiceLocator_ShouldIgnoreDuplicateRegistrations()
        {
            // 1. First Registration
            ServiceLocator.Register(_loop, new[] { typeof(SceneManagerTests).Assembly });
            
            int initialSystemCount = MockAutoRegSystem.UpdateCount;
            _loop.RunSingleTick();
            MockAutoRegSystem.UpdateCount.Should().Be(initialSystemCount + 1);

            // 2. Second Registration (Should not throw, should log warning/skip)
            Action act = () => ServiceLocator.Register(_loop, new[] { typeof(SceneManagerTests).Assembly });
            act.Should().NotThrow();

            // 3. Verify no duplicate systems
            MockAutoRegSystem.UpdateCount = 0;
            _loop.RunSingleTick();
            // If duplicated, it would be 2. If correctly skipped, it should be 1.
            MockAutoRegSystem.UpdateCount.Should().Be(1, "System should effectively be a singleton and not duplicated.");
        }

        [Fact]
        public void ServiceLocator_ShouldNotUnregisterGlobalSystems_WhenSceneUnloads()
        {
            // 1. Register Global System (Simulate Game Startup)
            // We ignore the return value here, simulating "Global" registration that we don't track for unloading
            ServiceLocator.Register(_loop, new[] { typeof(SceneManagerTests).Assembly });
            
            _loop.SystemRunner.HasSystem(typeof(MockAutoRegSystem)).Should().BeTrue("Global system should be registered.");

            // 2. Load Scene (Simulate Scene Enter)
            // Scene registers and KEEPS the registration handle
            var sceneRegistration = ServiceLocator.Register(_loop, new[] { typeof(SceneManagerTests).Assembly });
            
            // 3. Unload Scene (Simulate Scene Exit)
            // Scene unregisters ONLY what it registered
            ServiceLocator.Unregister(_loop, sceneRegistration);

            // 4. Verify Global System is STILL there
            _loop.SystemRunner.HasSystem(typeof(MockAutoRegSystem)).Should().BeTrue("Global system should NOT be removed by scene unload.");
        }

        // Mock Scene for testing
        private class MockScene : IScene
        {
            public bool OnEnterCalled { get; private set; }
            public bool OnExitCalled { get; private set; }
            public GameLoop? LoopPassedToEnter { get; private set; }
            
            private readonly IEnumerable<ISystem> _systems;

            public MockScene(IEnumerable<ISystem>? systems = null)
            {
                _systems = systems ?? new List<ISystem>();
            }

            public IEnumerable<ISystem> RegisterSystems(GameLoop loop)
            {
                return _systems;
            }

            public IEnumerable<IActionService> RegisterActionServices(GameLoop loop) => new List<IActionService>();
            public IEnumerable<IReactionService> RegisterReactionServices(GameLoop loop) => new List<IReactionService>();

            public void OnEnter(GameLoop loop)
            {
                OnEnterCalled = true;
                LoopPassedToEnter = loop;
            }

            public void OnExit(GameLoop loop)
            {
                OnExitCalled = true;
            }
        }

        private class MockServiceLocatorScene : IScene
        {
            private ServiceRegistration? _registration;

            public IEnumerable<ISystem> RegisterSystems(GameLoop loop) => new List<ISystem>();
            public IEnumerable<IActionService> RegisterActionServices(GameLoop loop) => new List<IActionService>();
            public IEnumerable<IReactionService> RegisterReactionServices(GameLoop loop) => new List<IReactionService>();

            public void OnEnter(GameLoop loop)
            {
                // Register only types from THIS assembly to avoid scanning everything
                _registration = ServiceLocator.Register(loop, new[] { typeof(SceneManagerTests).Assembly });
            }

            public void OnExit(GameLoop loop)
            {
                if (_registration != null)
                {
                    ServiceLocator.Unregister(loop, _registration);
                    _registration = null;
                }
            }
        }
    }

    public class MockSystem : ISystem
    {
        public int UpdateCount { get; private set; }
        public void Update(GlobalState state)
        {
            UpdateCount++;
        }
    }

    // Must be public for ServiceLocator to find it
    public class MockAutoRegSystem : ISystem
    {
        public static int UpdateCount { get; set; }
        public void Update(GlobalState state)
        {
            UpdateCount++;
        }
    }

    [NetworkId("00000000-0000-0000-0000-123456789000")]
    public struct MockAction : IAction
    {
    }

    public class MockActionService : ActionService<MockAction, TestComponent>
    {
        protected override void ExecuteProcess(MockAction action, ref TestComponent target, Context ctx)
        {
        }
    }

    [NetworkId("00000000-1111-2222-3333-444444444444")]
    public struct TestComponent : IComponent
    {
    }
}
