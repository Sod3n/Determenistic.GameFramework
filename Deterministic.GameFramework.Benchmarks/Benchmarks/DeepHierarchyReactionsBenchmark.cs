using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

/// <summary>
/// Critical benchmark: Deep hierarchy with reactions at each level
/// Tests reaction propagation up the tree - the core DAR feature
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class DeepHierarchyReactionsBenchmark
{
    private HierarchyRoot _depth1 = null!;
    private HierarchyRoot _depth3 = null!;
    private HierarchyRoot _depth5 = null!;
    private HierarchyRoot _depth10 = null!;
    private HierarchyAction _action = null!;

    [GlobalSetup]
    public void Setup()
    {
        _action = new HierarchyAction(5);
        
        // Depth 1: Root -> Leaf (1 reaction at leaf)
        _depth1 = CreateHierarchy(1);
        
        // Depth 3: Root -> L1 -> L2 -> Leaf (3 reactions total)
        _depth3 = CreateHierarchy(3);
        
        // Depth 5: Root -> L1 -> L2 -> L3 -> L4 -> Leaf (5 reactions)
        _depth5 = CreateHierarchy(5);
        
        // Depth 10: Very deep hierarchy (10 reactions)
        _depth10 = CreateHierarchy(10);
    }

    private HierarchyRoot CreateHierarchy(int depth)
    {
        var root = new HierarchyRoot();
        BranchDomain current = root;
        
        // Create chain: Root -> Level1 -> Level2 -> ... -> Leaf
        for (int i = 0; i < depth - 1; i++)
        {
            current = new HierarchyLevel(current, i);
        }
        
        // Add leaf at the end
        new HierarchyLeaf(current);
        
        return root;
    }

    [Benchmark(Baseline = true, Description = "Depth 1 (1 reaction)")]
    public void Depth1_1Reaction()
    {
        _action.Execute(_depth1);
    }

    [Benchmark(Description = "Depth 3 (3 reactions propagating up)")]
    public void Depth3_3Reactions()
    {
        _action.Execute(_depth3);
    }

    [Benchmark(Description = "Depth 5 (5 reactions propagating up)")]
    public void Depth5_5Reactions()
    {
        _action.Execute(_depth5);
    }

    [Benchmark(Description = "Depth 10 (10 reactions propagating up)")]
    public void Depth10_10Reactions()
    {
        _action.Execute(_depth10);
    }

    // Batch tests
    [Benchmark(Description = "1000 actions on depth 1")]
    public void ThousandActions_Depth1()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_depth1);
        }
    }

    [Benchmark(Description = "1000 actions on depth 5")]
    public void ThousandActions_Depth5()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_depth5);
        }
    }

    [Benchmark(Description = "1000 actions on depth 10")]
    public void ThousandActions_Depth10()
    {
        for (int i = 0; i < 1000; i++)
        {
            _action.Execute(_depth10);
        }
    }
}

public class HierarchyRoot : BranchDomain
{
    public int Value { get; set; }

    public HierarchyRoot() : base(null)
    {
        // Root has its own reaction
        new RootReaction(this).AddTo(Disposables);
    }
}

public class HierarchyLevel : BranchDomain
{
    private readonly int _level;

    public HierarchyLevel(BranchDomain parent, int level) : base(parent)
    {
        _level = level;
        // Each level has a reaction that will propagate up
        new LevelReaction(this, _level).AddTo(Disposables);
    }
}

public class HierarchyLeaf : BranchDomain
{
    public int LeafValue { get; set; }

    public HierarchyLeaf(BranchDomain parent) : base(parent)
    {
        // Leaf has a reaction
        new LeafReaction(this).AddTo(Disposables);
    }
}

public class HierarchyAction : DARAction<HierarchyLeaf, HierarchyAction>
{
    private readonly int _value;

    public HierarchyAction(int value)
    {
        _value = value;
    }

    protected override bool _IsExecutable(HierarchyLeaf domain)
    {
        return true;
    }

    protected override void ExecuteProcess(HierarchyLeaf domain)
    {
        domain.LeafValue += _value;
    }
}

public class RootReaction : Reaction, IAfterReaction<HierarchyLeaf, HierarchyAction>
{
    public RootReaction(BranchDomain domain) : base(domain) { }

    public void OnAfter(HierarchyLeaf domain, HierarchyAction action)
    {
        // Root reaction modifies value
        domain.LeafValue += 1;
    }
}

public class LevelReaction : Reaction, IAfterReaction<HierarchyLeaf, HierarchyAction>
{
    private readonly int _level;

    public LevelReaction(BranchDomain domain, int level) : base(domain)
    {
        _level = level;
    }

    public void OnAfter(HierarchyLeaf domain, HierarchyAction action)
    {
        // Each level adds its contribution
        domain.LeafValue += _level;
    }
}

public class LeafReaction : Reaction, IAfterReaction<HierarchyLeaf, HierarchyAction>
{
    public LeafReaction(BranchDomain domain) : base(domain) { }

    public void OnAfter(HierarchyLeaf domain, HierarchyAction action)
    {
        // Leaf reaction
        domain.LeafValue *= 2;
    }
}
