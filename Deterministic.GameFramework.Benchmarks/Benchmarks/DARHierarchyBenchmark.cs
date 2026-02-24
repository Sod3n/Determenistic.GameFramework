using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DARHierarchyBenchmark
{
    private RootDomain _flatDomain = null!;
    private RootDomain _deepDomain = null!;
    private SimpleHierarchyAction _action = null!;

    [GlobalSetup]
    public void Setup()
    {
        _action = new SimpleHierarchyAction(5);
        
        // Flat hierarchy: Root -> Child
        _flatDomain = new RootDomain();
        new ChildDomain(_flatDomain);
        
        // Deep hierarchy: Root -> Level1 -> Level2 -> Level3 -> Child
        _deepDomain = new RootDomain();
        var level1 = new MiddleDomain(_deepDomain);
        var level2 = new MiddleDomain(level1);
        var level3 = new MiddleDomain(level2);
        new ChildDomain(level3);
    }

    [Benchmark(Baseline = true, Description = "Action on flat hierarchy (2 levels)")]
    public void FlatHierarchy()
    {
        _action.Execute(_flatDomain);
    }

    [Benchmark(Description = "Action on deep hierarchy (5 levels)")]
    public void DeepHierarchy()
    {
        _action.Execute(_deepDomain);
    }

    [Benchmark(Description = "GetFirst<T> on flat hierarchy")]
    public void GetFirstFlat()
    {
        var child = _flatDomain.GetFirst<ChildDomain>();
    }

    [Benchmark(Description = "GetFirst<T> on deep hierarchy")]
    public void GetFirstDeep()
    {
        var child = _deepDomain.GetFirst<ChildDomain>();
    }
}

public class RootDomain : BranchDomain
{
    public int Value { get; set; }

    public RootDomain() : base(null) { }
}

public class MiddleDomain : BranchDomain
{
    public MiddleDomain(BranchDomain parent) : base(parent) { }
}

public class ChildDomain : BranchDomain
{
    public int ChildValue { get; set; }

    public ChildDomain(BranchDomain parent) : base(parent)
    {
        new ChildReaction(this).AddTo(Disposables);
    }
}

public class SimpleHierarchyAction : DARAction<ChildDomain, SimpleHierarchyAction>
{
    private readonly int _value;

    public SimpleHierarchyAction(int value)
    {
        _value = value;
    }

    protected override bool _IsExecutable(ChildDomain domain)
    {
        return true;
    }

    protected override void ExecuteProcess(ChildDomain domain)
    {
        domain.ChildValue += _value;
    }
}

public class ChildReaction : Reaction, IAfterReaction<ChildDomain, SimpleHierarchyAction>
{
    public ChildReaction(BranchDomain domain) : base(domain) { }

    public void OnAfter(ChildDomain domain, SimpleHierarchyAction action)
    {
        domain.ChildValue *= 2;
    }
}
