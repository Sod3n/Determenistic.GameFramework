using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

/// <summary>
/// Critical benchmark: How do reactions scale?
/// - 1 action with N reactions (1, 5, 10, 20, 50)
/// - N actions with 1 reaction each
/// - Deep hierarchy with reactions at each level
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ReactionScalingBenchmark
{
    private ScalableDomain _domain1Reaction = null!;
    private ScalableDomain _domain5Reactions = null!;
    private ScalableDomain _domain10Reactions = null!;
    private ScalableDomain _domain20Reactions = null!;
    private ScalableDomain _domain50Reactions = null!;
    private ScalableAction _action = null!;

    [GlobalSetup]
    public void Setup()
    {
        _action = new ScalableAction(10);
        
        _domain1Reaction = new ScalableDomain(1);
        _domain5Reactions = new ScalableDomain(5);
        _domain10Reactions = new ScalableDomain(10);
        _domain20Reactions = new ScalableDomain(20);
        _domain50Reactions = new ScalableDomain(50);
    }

    // ========================================================================
    // 1 Action with N Reactions
    // ========================================================================

    [Benchmark(Baseline = true, Description = "1 action with 1 reaction")]
    public void OneAction_1Reaction()
    {
        _action.Execute(_domain1Reaction);
    }

    [Benchmark(Description = "1 action with 5 reactions")]
    public void OneAction_5Reactions()
    {
        _action.Execute(_domain5Reactions);
    }

    [Benchmark(Description = "1 action with 10 reactions")]
    public void OneAction_10Reactions()
    {
        _action.Execute(_domain10Reactions);
    }

    [Benchmark(Description = "1 action with 20 reactions")]
    public void OneAction_20Reactions()
    {
        _action.Execute(_domain20Reactions);
    }

    [Benchmark(Description = "1 action with 50 reactions")]
    public void OneAction_50Reactions()
    {
        _action.Execute(_domain50Reactions);
    }

    // ========================================================================
    // 1000 Actions with N Reactions (batch test)
    // ========================================================================

    [Benchmark(Description = "1000 actions with 1 reaction")]
    public void ThousandActions_1Reaction()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain1Reaction);
        }
    }

    [Benchmark(Description = "1000 actions with 5 reactions")]
    public void ThousandActions_5Reactions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain5Reactions);
        }
    }

    [Benchmark(Description = "1000 actions with 10 reactions")]
    public void ThousandActions_10Reactions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain10Reactions);
        }
    }

    [Benchmark(Description = "1000 actions with 20 reactions")]
    public void ThousandActions_20Reactions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain20Reactions);
        }
    }

    [Benchmark(Description = "1000 actions with 50 reactions")]
    public void ThousandActions_50Reactions()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_domain50Reactions);
        }
    }
}

public class ScalableDomain : BranchDomain
{
    public int Value { get; set; }

    public ScalableDomain(int reactionCount) : base(null)
    {
        // Add N reactions to this domain
        for (int i = 0; i < reactionCount; i++)
        {
            new ScalableReaction(this, i).AddTo(Disposables);
        }
    }
}

public class ScalableAction : DARAction<ScalableDomain, ScalableAction>
{
    private readonly int _value;

    public ScalableAction(int value)
    {
        _value = value;
    }

    protected override bool _IsExecutable(ScalableDomain domain)
    {
        return true;
    }

    protected override void ExecuteProcess(ScalableDomain domain)
    {
        domain.Value += _value;
    }
}

public class ScalableReaction : Reaction, IAfterReaction<ScalableDomain, ScalableAction>
{
    private readonly int _id;

    public ScalableReaction(BranchDomain domain, int id) : base(domain)
    {
        _id = id;
    }

    public void OnAfter(ScalableDomain domain, ScalableAction action)
    {
        // Minimal work - just modify value
        domain.Value += _id;
    }
}
