using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Benchmarks.DAR;

[MemoryDiagnoser]
public class SchedulerBenchmarks
{
    private ActionScheduler _scheduler;
    private Entity _entity;
    private BenchAction _action;
    private DenseComponentId _actionId;

    [GlobalSetup]
    public void Setup()
    {
        try 
        {
            ComponentId.RegisterAssembly(typeof(BenchAction).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }
        catch { }

        _scheduler = new ActionScheduler();
        _entity = new Entity(1);
        _action = new BenchAction { Amount = 10 };
        _actionId = ComponentId<BenchAction>.DenseId;
    }

    [Benchmark]
    public void Schedule()
    {
        _scheduler.Schedule(_action, _actionId, _entity, 100);
    }
}
