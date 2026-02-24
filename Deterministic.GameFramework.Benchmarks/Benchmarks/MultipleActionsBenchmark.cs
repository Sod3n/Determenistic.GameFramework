using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

/// <summary>
/// Critical benchmark: Many different actions with reactions
/// Tests how DAR handles multiple action types on same domain
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class MultipleActionsBenchmark
{
    private MultiActionDomain _domain = null!;
    private ActionA _actionA = null!;
    private ActionB _actionB = null!;
    private ActionC _actionC = null!;
    private ActionD _actionD = null!;
    private ActionE _actionE = null!;

    [GlobalSetup]
    public void Setup()
    {
        _domain = new MultiActionDomain();
        _actionA = new ActionA(1);
        _actionB = new ActionB(2);
        _actionC = new ActionC(3);
        _actionD = new ActionD(4);
        _actionE = new ActionE(5);
    }

    [Benchmark(Baseline = true, Description = "1 action type executed 1000 times")]
    public void OneActionType_1000Times()
    {
        for (int i = 0; i < 1000; i++)
        {
            _actionA.Execute(_domain);
        }
    }

    [Benchmark(Description = "5 action types executed 200 times each (1000 total)")]
    public void FiveActionTypes_200TimesEach()
    {
        for (int i = 0; i < 200; i++)
        {
            _actionA.Execute(_domain);
            _actionB.Execute(_domain);
            _actionC.Execute(_domain);
            _actionD.Execute(_domain);
            _actionE.Execute(_domain);
        }
    }

    [Benchmark(Description = "5 action types interleaved (1000 total)")]
    public void FiveActionTypes_Interleaved()
    {
        for (int i = 0; i < 1000; i++)
        {
            switch (i % 5)
            {
                case 0: _actionA.Execute(_domain); break;
                case 1: _actionB.Execute(_domain); break;
                case 2: _actionC.Execute(_domain); break;
                case 3: _actionD.Execute(_domain); break;
                case 4: _actionE.Execute(_domain); break;
            }
        }
    }
}

public class MultiActionDomain : BranchDomain
{
    public int ValueA { get; set; }
    public int ValueB { get; set; }
    public int ValueC { get; set; }
    public int ValueD { get; set; }
    public int ValueE { get; set; }

    public MultiActionDomain() : base(null)
    {
        // Each action type has its own reaction
        new ReactionForA(this).AddTo(Disposables);
        new ReactionForB(this).AddTo(Disposables);
        new ReactionForC(this).AddTo(Disposables);
        new ReactionForD(this).AddTo(Disposables);
        new ReactionForE(this).AddTo(Disposables);
    }
}

// 5 different action types
public class ActionA : DARAction<MultiActionDomain, ActionA>
{
    private readonly int _value;
    public ActionA(int value) => _value = value;
    protected override bool _IsExecutable(MultiActionDomain domain) => true;
    protected override void ExecuteProcess(MultiActionDomain domain) => domain.ValueA += _value;
}

public class ActionB : DARAction<MultiActionDomain, ActionB>
{
    private readonly int _value;
    public ActionB(int value) => _value = value;
    protected override bool _IsExecutable(MultiActionDomain domain) => true;
    protected override void ExecuteProcess(MultiActionDomain domain) => domain.ValueB += _value;
}

public class ActionC : DARAction<MultiActionDomain, ActionC>
{
    private readonly int _value;
    public ActionC(int value) => _value = value;
    protected override bool _IsExecutable(MultiActionDomain domain) => true;
    protected override void ExecuteProcess(MultiActionDomain domain) => domain.ValueC += _value;
}

public class ActionD : DARAction<MultiActionDomain, ActionD>
{
    private readonly int _value;
    public ActionD(int value) => _value = value;
    protected override bool _IsExecutable(MultiActionDomain domain) => true;
    protected override void ExecuteProcess(MultiActionDomain domain) => domain.ValueD += _value;
}

public class ActionE : DARAction<MultiActionDomain, ActionE>
{
    private readonly int _value;
    public ActionE(int value) => _value = value;
    protected override bool _IsExecutable(MultiActionDomain domain) => true;
    protected override void ExecuteProcess(MultiActionDomain domain) => domain.ValueE += _value;
}

// Reactions for each action type
public class ReactionForA : Reaction, IAfterReaction<MultiActionDomain, ActionA>
{
    public ReactionForA(BranchDomain domain) : base(domain) { }
    public void OnAfter(MultiActionDomain domain, ActionA action) => domain.ValueA *= 2;
}

public class ReactionForB : Reaction, IAfterReaction<MultiActionDomain, ActionB>
{
    public ReactionForB(BranchDomain domain) : base(domain) { }
    public void OnAfter(MultiActionDomain domain, ActionB action) => domain.ValueB *= 2;
}

public class ReactionForC : Reaction, IAfterReaction<MultiActionDomain, ActionC>
{
    public ReactionForC(BranchDomain domain) : base(domain) { }
    public void OnAfter(MultiActionDomain domain, ActionC action) => domain.ValueC *= 2;
}

public class ReactionForD : Reaction, IAfterReaction<MultiActionDomain, ActionD>
{
    public ReactionForD(BranchDomain domain) : base(domain) { }
    public void OnAfter(MultiActionDomain domain, ActionD action) => domain.ValueD *= 2;
}

public class ReactionForE : Reaction, IAfterReaction<MultiActionDomain, ActionE>
{
    public ReactionForE(BranchDomain domain) : base(domain) { }
    public void OnAfter(MultiActionDomain domain, ActionE action) => domain.ValueE *= 2;
}
