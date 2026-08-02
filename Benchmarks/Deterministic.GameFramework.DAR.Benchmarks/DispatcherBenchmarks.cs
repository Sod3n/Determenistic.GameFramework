using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Benchmarks.DAR;

[MemoryDiagnoser]
public class DispatcherBenchmarks
{
    private ReactionDispatcher _dispatcher;
    private EntityWorld _world;
    private ActionScheduler _scheduler;
    private DenseComponentId _actionId;
    private BenchAction _action;
    private Entity[] _entities;
    
    [Params(100, 1000)]
    public int EntityCount;

    [GlobalSetup]
    public void Setup()
    {
        try
        {
            ComponentId.RegisterAssembly(typeof(BenchAction).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }
        catch { }

        _world = new EntityWorld();
        _dispatcher = new ReactionDispatcher();
        _scheduler = new ActionScheduler();

        var actionService = new BenchActionService();
        var reaction = new BenchReactionService();

        _dispatcher.RegisterServices(
            new IActionService[] { actionService },
            new IReactionService[] { reaction }
        );
        _dispatcher.EnableAction(actionService);
        _dispatcher.EnableReaction(reaction);
        _dispatcher.ActionDispatcher = new MockActionDispatcher();

        _actionId = (DenseComponentId)_dispatcher.GetDenseId<BenchAction>();
        _action = new BenchAction { Amount = 1 };
        
        _entities = new Entity[EntityCount];
        for (int i = 0; i < EntityCount; i++)
        {
            _entities[i] = _world.CreateEntity();
            _world.AddComponent(_entities[i], new BenchComponent { Value = 0 });
        }
    }

    [Benchmark]
    public void FullCycle()
    {
        // 1. Schedule Actions
        for (int i = 0; i < EntityCount; i++)
        {
            _scheduler.Schedule(_action, _actionId, _entities[i], 10);
        }

        // 2. Execute Actions (Inject into World)
        _scheduler.ExecuteActions(10, _world, _dispatcher);

        // 3. Process Systems (Run Logic)
        _dispatcher.Update(_world);
        
        // 4. Cleanup
        _scheduler.PruneHistory(11); // Clear executed actions (tick 10 < 11)
        _world.ClearDirty();
    }
}
