using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Benchmarks.Benchmarks;

/// <summary>
/// Compares DAR pattern against Traditional OOP and Event-driven approaches
/// for implementing game mechanics (status effects modifying actions)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class ComparisonBenchmark
{
    private DARApproach _darApproach = null!;
    private TraditionalApproach _traditionalApproach = null!;
    private EventDrivenApproach _eventDrivenApproach = null!;

    [GlobalSetup]
    public void Setup()
    {
        _darApproach = new DARApproach();
        _traditionalApproach = new TraditionalApproach();
        _eventDrivenApproach = new EventDrivenApproach();
    }

    [Benchmark(Baseline = true, Description = "Traditional OOP (5 status checks)")]
    public void Traditional_5StatusEffects()
    {
        for (int i = 0; i < 1000; i++)
        {
            _traditionalApproach.ExecuteAttack();
        }
    }

    [Benchmark(Description = "Event-driven (5 event handlers)")]
    public void EventDriven_5StatusEffects()
    {
        for (int i = 0; i < 1000; i++)
        {
            _eventDrivenApproach.ExecuteAttack();
        }
    }

    [Benchmark(Description = "DAR (5 reactions)")]
    public void DAR_5StatusEffects()
    {
        for (int i = 0; i < 1000; i++)
        {
            _darApproach.ExecuteAttack();
        }
    }
}

// ============================================================================
// Traditional OOP Approach
// ============================================================================

public class TraditionalApproach
{
    private readonly TraditionalCharacter _character;

    public TraditionalApproach()
    {
        _character = new TraditionalCharacter
        {
            HasStrength = true,
            HasProtection = true,
            HasVulnerable = true,
            HasWeakness = true,
            HasNumbness = false
        };
    }

    public void ExecuteAttack()
    {
        int damage = 10;

        // Check all status effects (typical imperative approach)
        if (_character.HasStrength)
        {
            damage = (int)(damage * 1.5f);
        }

        if (_character.HasWeakness)
        {
            damage = (int)(damage * 0.75f);
        }

        if (_character.HasVulnerable)
        {
            damage = (int)(damage * 1.5f);
        }

        if (_character.HasProtection)
        {
            damage = (int)(damage * 0.8f);
        }

        if (_character.HasNumbness && Random.Shared.Next(100) < 50)
        {
            return; // Cancel action
        }

        _character.Health -= damage;
    }
}

public class TraditionalCharacter
{
    public int Health { get; set; } = 100;
    public bool HasStrength { get; set; }
    public bool HasProtection { get; set; }
    public bool HasVulnerable { get; set; }
    public bool HasWeakness { get; set; }
    public bool HasNumbness { get; set; }
}

// ============================================================================
// Event-driven Approach
// ============================================================================

public class EventDrivenApproach
{
    private readonly EventBus _eventBus;
    private readonly EventCharacter _character;

    public EventDrivenApproach()
    {
        _eventBus = new EventBus();
        _character = new EventCharacter { Health = 100 };

        // Subscribe handlers
        _eventBus.Subscribe<AttackEvent>(OnStrength);
        _eventBus.Subscribe<AttackEvent>(OnWeakness);
        _eventBus.Subscribe<AttackEvent>(OnVulnerable);
        _eventBus.Subscribe<AttackEvent>(OnProtection);
        _eventBus.Subscribe<AttackEvent>(OnNumbness);
    }

    public void ExecuteAttack()
    {
        var attackEvent = new AttackEvent { Damage = 10, Cancelled = false };
        _eventBus.Publish(attackEvent);

        if (!attackEvent.Cancelled)
        {
            _character.Health -= attackEvent.Damage;
        }
    }

    private void OnStrength(AttackEvent e) => e.Damage = (int)(e.Damage * 1.5f);
    private void OnWeakness(AttackEvent e) => e.Damage = (int)(e.Damage * 0.75f);
    private void OnVulnerable(AttackEvent e) => e.Damage = (int)(e.Damage * 1.5f);
    private void OnProtection(AttackEvent e) => e.Damage = (int)(e.Damage * 0.8f);
    private void OnNumbness(AttackEvent e)
    {
        if (Random.Shared.Next(100) < 50)
            e.Cancelled = true;
    }
}

public class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();
        _handlers[type].Add(handler);
    }

    public void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (_handlers.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers)
            {
                ((Action<T>)handler)(eventData);
            }
        }
    }
}

public class AttackEvent
{
    public int Damage { get; set; }
    public bool Cancelled { get; set; }
}

public class EventCharacter
{
    public int Health { get; set; }
}

// ============================================================================
// DAR Approach
// ============================================================================

public class DARApproach
{
    private readonly DARCharacter _character;
    private readonly DARAttackAction _action;

    public DARApproach()
    {
        _character = new DARCharacter();
        _action = new DARAttackAction(10);
    }

    public void ExecuteAttack()
    {
        _action.Execute(_character);
    }
}

public class DARCharacter : BranchDomain
{
    public int Health { get; set; } = 100;

    public DARCharacter() : base(null)
    {
        // Status effects as independent domains with reactions
        Subdomains.Add(new StrengthStatus(this));
        Subdomains.Add(new WeaknessStatus(this));
        Subdomains.Add(new VulnerableStatus(this));
        Subdomains.Add(new ProtectionStatus(this));
        // Numbness disabled for fair comparison (random would affect benchmarks)
        // Subdomains.Add(new NumbnessStatus(this));
    }
}

public class DARAttackAction : DARAction<DARCharacter, DARAttackAction>
{
    private readonly int _baseDamage;
    public int Damage { get; set; }

    public DARAttackAction(int baseDamage)
    {
        _baseDamage = baseDamage;
    }

    protected override bool _IsExecutable(DARCharacter domain)
    {
        Damage = _baseDamage;
        return true;
    }

    protected override void ExecuteProcess(DARCharacter domain)
    {
        domain.Health -= Damage;
    }
}

public class StrengthStatus : BranchDomain
{
    public StrengthStatus(BranchDomain parent) : base(parent)
    {
        new StrengthReaction(this).AddTo(Disposables);
    }

    private class StrengthReaction : Reaction, IPrepareReaction<DARCharacter, DARAttackAction>
    {
        public StrengthReaction(BranchDomain domain) : base(domain) { }

        public void OnPrepare(DARCharacter domain, DARAttackAction action)
        {
            action.Damage = (int)(action.Damage * 1.5f);
        }
    }
}

public class WeaknessStatus : BranchDomain
{
    public WeaknessStatus(BranchDomain parent) : base(parent)
    {
        new WeaknessReaction(this).AddTo(Disposables);
    }

    private class WeaknessReaction : Reaction, IPrepareReaction<DARCharacter, DARAttackAction>
    {
        public WeaknessReaction(BranchDomain domain) : base(domain) { }

        public void OnPrepare(DARCharacter domain, DARAttackAction action)
        {
            action.Damage = (int)(action.Damage * 0.75f);
        }
    }
}

public class VulnerableStatus : BranchDomain
{
    public VulnerableStatus(BranchDomain parent) : base(parent)
    {
        new VulnerableReaction(this).AddTo(Disposables);
    }

    private class VulnerableReaction : Reaction, IPrepareReaction<DARCharacter, DARAttackAction>
    {
        public VulnerableReaction(BranchDomain domain) : base(domain) { }

        public void OnPrepare(DARCharacter domain, DARAttackAction action)
        {
            action.Damage = (int)(action.Damage * 1.5f);
        }
    }
}

public class ProtectionStatus : BranchDomain
{
    public ProtectionStatus(BranchDomain parent) : base(parent)
    {
        new ProtectionReaction(this).AddTo(Disposables);
    }

    private class ProtectionReaction : Reaction, IPrepareReaction<DARCharacter, DARAttackAction>
    {
        public ProtectionReaction(BranchDomain domain) : base(domain) { }

        public void OnPrepare(DARCharacter domain, DARAttackAction action)
        {
            action.Damage = (int)(action.Damage * 0.8f);
        }
    }
}
