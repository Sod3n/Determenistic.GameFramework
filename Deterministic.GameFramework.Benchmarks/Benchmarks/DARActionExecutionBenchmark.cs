using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DARActionExecutionBenchmark
{
    private TestDomain _domain = null!;
    private TestDomainWithReactions _domainWithReactions = null!;
    private SimpleAction _action = null!;

    [GlobalSetup]
    public void Setup()
    {
        _domain = new TestDomain();
        _domainWithReactions = new TestDomainWithReactions();
        _action = new SimpleAction(10);
    }

    [Benchmark(Baseline = true, Description = "Action without reactions")]
    public void ActionWithoutReactions()
    {
        _action.Execute(_domain);
    }

    [Benchmark(Description = "Action with 1 After reaction")]
    public void ActionWith1Reaction()
    {
        _action.Execute(_domainWithReactions);
    }

    [Benchmark(Description = "Action with 5 reactions (all phases)")]
    public void ActionWith5Reactions()
    {
        var domain = new TestDomainWith5Reactions();
        _action.Execute(domain);
    }

    [Benchmark(Description = "1000 actions without reactions")]
    public void Thousand_ActionsWithoutReactions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain);
        }
    }

    [Benchmark(Description = "1000 actions with 1 reaction")]
    public void Thousand_ActionsWith1Reaction()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domainWithReactions);
        }
    }

    [Benchmark(Description = "1000 actions with 5 reactions")]
    public void Thousand_ActionsWith5Reactions()
    {
        var domain = new TestDomainWith5Reactions();
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(domain);
        }
    }
}

public class TestDomain : BranchDomain
{
    public int Value { get; set; }

    public TestDomain() : base(null) { }
}

public class TestDomainWithReactions : BranchDomain
{
    public int Value { get; set; }

    public TestDomainWithReactions() : base(null)
    {
        new TestAfterReaction(this).AddTo(Disposables);
    }
}

public class TestDomainWith5Reactions : BranchDomain
{
    public int Value { get; set; }

    public TestDomainWith5Reactions() : base(null)
    {
        new TestPrepareReaction(this).AddTo(Disposables);
        new TestAbortReaction(this).AddTo(Disposables);
        new TestBeforeReaction(this).AddTo(Disposables);
        new TestAfterReaction(this).AddTo(Disposables);
        new TestAfterReaction2(this).AddTo(Disposables);
    }
}

public class SimpleAction : DARAction<TestDomain, SimpleAction>
{
    private readonly int _value;

    public SimpleAction(int value)
    {
        _value = value;
    }

    protected override bool _IsExecutable(TestDomain domain)
    {
        return true;
    }

    protected override void ExecuteProcess(TestDomain domain)
    {
        domain.Value += _value;
    }
}

public class TestPrepareReaction : Reaction, IPrepareReaction<TestDomain, SimpleAction>
{
    public TestPrepareReaction(BranchDomain domain) : base(domain) { }

    public void OnPrepare(TestDomain domain, SimpleAction action)
    {
        // Minimal work
    }
}

public class TestAbortReaction : Reaction, IAbortReaction<TestDomain, SimpleAction>
{
    public TestAbortReaction(BranchDomain domain) : base(domain) { }

    public bool OnAbort(TestDomain domain, SimpleAction action)
    {
        return false; // Never abort
    }
}

public class TestBeforeReaction : Reaction, IBeforeReaction<TestDomain, SimpleAction>
{
    public TestBeforeReaction(BranchDomain domain) : base(domain) { }

    public void OnBefore(TestDomain domain, SimpleAction action)
    {
        // Minimal work
    }
}

public class TestAfterReaction : Reaction, IAfterReaction<TestDomain, SimpleAction>
{
    public TestAfterReaction(BranchDomain domain) : base(domain) { }

    public void OnAfter(TestDomain domain, SimpleAction action)
    {
        // Minimal work
        domain.Value *= 2;
    }
}

public class TestAfterReaction2 : Reaction, IAfterReaction<TestDomain, SimpleAction>
{
    public TestAfterReaction2(BranchDomain domain) : base(domain) { }

    public void OnAfter(TestDomain domain, SimpleAction action)
    {
        // Minimal work
        domain.Value += 1;
    }
}
