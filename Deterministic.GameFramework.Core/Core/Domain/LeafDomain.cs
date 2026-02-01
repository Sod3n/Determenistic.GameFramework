using JetBrains.Annotations;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Utils;

/// <summary>
/// Base domain class that can have a parent but no children.
/// Use this for leaf domains like ObservableAttribute, ObservableAttributeList, etc.
/// </summary>
public class LeafDomain : IDomain, IDisposable, INode<LeafDomain>, ICacheInvalidatable
{
	public int Id { get; set; } // Numeric ID inside some context (For example, in game state)
	
	protected internal LeafDomain? _parentDomain;

	public readonly CompositeDisposable Disposables = new();
	private bool _disposed = false;

	// Tree cache system
	protected internal readonly TreeCacheManager<LeafDomain> CacheManager;

	// Unified cached lists - store local items and aggregate from parent chain
	// Order matches reaction execution: Prepare -> Abort -> Before -> After
	public readonly CachedList<LeafDomain, IHasPrepareReaction> PrepareReactions;
	public readonly CachedList<LeafDomain, IHasAbortReaction> AbortReactions;
	public readonly CachedList<LeafDomain, IHasBeforeReaction> BeforeReactions;
	public readonly CachedList<LeafDomain, IHasAfterReaction> AfterReactions;

	public LeafDomain(BranchDomain? parent) 
	{
		CacheManager = new TreeCacheManager<LeafDomain>(this);

		// Create unified cached lists - they store local items AND aggregate from tree
		// The extractor returns the node's local items (not the aggregated cache)
		PrepareReactions = new CachedList<LeafDomain, IHasPrepareReaction>(
			this,
			node => node.PrepareReactions.GetLocalItems(),
			() => CacheManager.InvalidateAllRecursive()
		);
		AbortReactions = new CachedList<LeafDomain, IHasAbortReaction>(
			this,
			node => node.AbortReactions.GetLocalItems(),
			() => CacheManager.InvalidateAllRecursive()
		);
		BeforeReactions = new CachedList<LeafDomain, IHasBeforeReaction>(
			this,
			node => node.BeforeReactions.GetLocalItems(),
			() => CacheManager.InvalidateAllRecursive()
		);
		AfterReactions = new CachedList<LeafDomain, IHasAfterReaction>(
			this,
			node => node.AfterReactions.GetLocalItems(),
			() => CacheManager.InvalidateAllRecursive()
		);

		// Register cached lists with manager for invalidation
		CacheManager.RegisterInvalidatable(PrepareReactions.Invalidate);
		CacheManager.RegisterInvalidatable(AbortReactions.Invalidate);
		CacheManager.RegisterInvalidatable(BeforeReactions.Invalidate);
		CacheManager.RegisterInvalidatable(AfterReactions.Invalidate);
		
		parent?.Subdomains.Add(this);
	}


	// ---- INode Implementation ---- //

	public LeafDomain? NodeGetParent()
	{
		return _parentDomain;
	}
	
	// if it is parent that it is Domain
	public BranchDomain? GetParent()
	{
		return _parentDomain as BranchDomain;
	}

	public virtual IEnumerable<LeafDomain> GetChildren() => Enumerable.Empty<LeafDomain>();

	// ---- ICacheInvalidatable Implementation ---- //

	public void InvalidateAllCaches()
	{
		CacheManager?.InvalidateAllRecursive();
	}

	public IDARAction RevertAction(IDARAction action)
	{
		if (action.IsRevertable(this))
			action.RevertProcess(this);
		return action;
	}

	public void CollectPrepareAll(IDARAction action)
	{
		// PrepareReactions already contains aggregated items from tree
		action.PrepareReactions.AddRange(PrepareReactions);
	}

	public void CollectAbortAll(IDARAction action)
	{
		// AbortReactions already contains aggregated items from tree
		action.AbortReactions.AddRange(AbortReactions);
	}

	public void CollectBeforeAll(IDARAction action)
	{
		// BeforeReactions already contains aggregated items from tree
		action.BeforeReactions.AddRange(BeforeReactions);
	}

	public void CollectAfterAll(IDARAction action)
	{
		// AfterReactions already contains aggregated items from tree
		action.AfterReactions.AddRange(AfterReactions);
	}

	public int GetTotalReactionCount()
	{
		return PrepareReactions.Count + AbortReactions.Count + BeforeReactions.Count + AfterReactions.Count;
	}

	public bool HasReaction(IReaction reaction)
	{
		return PrepareReactions.Contains(reaction) ||
		       AbortReactions.Contains(reaction) ||
		       BeforeReactions.Contains(reaction) ||
		       AfterReactions.Contains(reaction);
	}
	
	public void RemoveFromParent()
	{
		GetParent()?.Subdomains.Remove(this);
	}

	public T? GetInParent<T>(bool includeSelf = false) where T : class
	{
		var current = includeSelf ? this : _parentDomain;
		
		while (current != null)
		{
			if (current is T typedDomain)
				return typedDomain;
			current = current._parentDomain;
		}
		
		return null;
	}
	
	public virtual T? GetFirst<T>(bool includeSelf = false) where T : class
	{
		var array = GetAll<T>(includeSelf);
		return array.Count > 0 ? array[0] : null;
	}

	public virtual List<T> GetAll<T>(bool includeSelf = false, bool recursive = true) where T : class
	{
		var result = new List<T>();
		if (includeSelf && this is T selfTyped)
			result.Add(selfTyped);
		
		// LeafDomain has no subdomains, so this is the complete implementation
		return result;
	}

	public virtual void Dispose()
	{
		if (_disposed) return;

		RemoveFromParent();
		// Dispose managed resources
		Disposables.Dispose();
		_disposed = true;
	}
}
