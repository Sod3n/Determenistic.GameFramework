using System;
using JetBrains.Annotations;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Delegate-based reaction for simple inline reactions.
/// Use this for quick, simple reactions. For complex logic, inherit from Reaction directly.
/// IMPORTANT: This does NOT auto-register. You must call .Prepare(), .Abort(), .Before(), or .After() 
/// followed by .AddTo(Disposables) to register and enable cleanup.
/// </summary>
[MustDisposeResource]
public sealed class Reaction<TDomain, TAction> : IReaction,
    IPrepareReaction<TDomain, TAction>,
    IAbortReaction<TDomain, TAction>,
    IBeforeReaction<TDomain, TAction>,
    IAfterReaction<TDomain, TAction>
    where TDomain : LeafDomain
    where TAction : IDARAction
{
    private Action<TDomain, TAction>? _prepareCallback;
    private Func<TDomain, TAction, bool>? _abortCallback;
    private Action<TDomain, TAction>? _beforeCallback;
    private Action<TDomain, TAction>? _afterCallback;

    public LeafDomain Target { get; private set; }

    public Reaction(LeafDomain target)
    {
        Target = target;
    }

    /// <summary>
    /// Register a callback to execute during prepare phase (before abort check).
    /// </summary>
    public Reaction<TDomain, TAction> Prepare(Action<TDomain, TAction> callback)
    {
        _prepareCallback = callback;
        Target.PrepareReactions.Add(this);
        return this;
    }

    /// <summary>
    /// Register a callback to check if action should abort.
    /// Return true to abort the action.
    /// </summary>
    public Reaction<TDomain, TAction> Abort(Func<TDomain, TAction, bool> callback)
    {
        _abortCallback = callback;
        Target.AbortReactions.Add(this);
        return this;
    }

    /// <summary>
    /// Register a callback to execute before the action.
    /// </summary>
    public Reaction<TDomain, TAction> Before(Action<TDomain, TAction> callback)
    {
        _beforeCallback = callback;
        Target.BeforeReactions.Add(this);
        return this;
    }

    /// <summary>
    /// Register a callback to execute after the action.
    /// </summary>
    public Reaction<TDomain, TAction> After(Action<TDomain, TAction> callback)
    {
        _afterCallback = callback;
        Target.AfterReactions.Add(this);
        return this;
    }

    void IPrepareReaction<TDomain, TAction>.OnPrepare(TDomain domain, TAction action)
    {
        _prepareCallback?.Invoke(domain, action);
    }

    bool IAbortReaction<TDomain, TAction>.OnAbort(TDomain domain, TAction action)
    {
        return _abortCallback?.Invoke(domain, action) ?? false;
    }

    void IBeforeReaction<TDomain, TAction>.OnBefore(TDomain domain, TAction action)
    {
        _beforeCallback?.Invoke(domain, action);
    }

    void IAfterReaction<TDomain, TAction>.OnAfter(TDomain domain, TAction action)
    {
        _afterCallback?.Invoke(domain, action);
    }

    public void Dispose()
    {
        if (Target == null) return;
        
        if (_prepareCallback != null)
            Target.PrepareReactions.Remove(this);
        if (_abortCallback != null)
            Target.AbortReactions.Remove(this);
        if (_beforeCallback != null)
            Target.BeforeReactions.Remove(this);
        if (_afterCallback != null)
            Target.AfterReactions.Remove(this);
        
        Target = null;
    }

    [HandlesResourceDisposal]
    public Reaction<TDomain, TAction> AddTo(CompositeDisposable disposables)
    {
        disposables.Add(this);
        return this;
    }
}
