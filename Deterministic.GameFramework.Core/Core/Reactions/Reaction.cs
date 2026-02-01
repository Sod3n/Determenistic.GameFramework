using JetBrains.Annotations;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Base reaction class. Inherit from this and implement one or more of:
/// - IPrepareReaction&lt;TDomain, TAction&gt;
/// - IAbortReaction&lt;TDomain, TAction&gt;
/// - IBeforeReaction&lt;TDomain, TAction&gt;
/// - IAfterReaction&lt;TDomain, TAction&gt;
/// The system automatically detects which interfaces you implement.
/// </summary>
[MustDisposeResource]
public abstract class Reaction : IReaction
{
    public LeafDomain Target { get; private set; }
    
    private IHasPrepareReaction? _prepare;
    private IHasAbortReaction? _abort;
    private IHasBeforeReaction? _before;
    private IHasAfterReaction? _after;

    protected Reaction(LeafDomain target)
    {
        Target = target;
        _prepare = this as IHasPrepareReaction;
        _abort = this as IHasAbortReaction;
        _before = this as IHasBeforeReaction;
        _after = this as IHasAfterReaction;

        if (_prepare != null)
            Target?.PrepareReactions.Add(_prepare);
        if (_abort != null)
            Target?.AbortReactions.Add(_abort);
        if (_before != null)
            Target?.BeforeReactions.Add(_before);
        if (_after != null)
            Target?.AfterReactions.Add(_after);
    }
    
    public void Dispose()
    {
        if (Target == null) return;
        
        RemoveFromTarget();
        Target = null;
    }
    
    private void RemoveFromTarget()
    {
        Target?.PrepareReactions.Remove(_prepare);
        Target?.AbortReactions.Remove(_abort);
        Target?.BeforeReactions.Remove(_before);
        Target?.AfterReactions.Remove(_after);
    }
    
    [HandlesResourceDisposal]
    public Reaction AddTo(CompositeDisposable disposables)
    {
        disposables.Add(this);
        return this;
    }
}
