using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Manages multiple tree caches for a node, handling invalidation cascading
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class TreeCacheManager<TNode>(TNode owner)
	where TNode : class, INode<TNode>
{
	private readonly List<Action> _cacheInvalidators = new();

	public CacheProperty<TNode, TValue> CreateProperty<TValue>(
		Func<TNode, TValue> valueExtractor,
		Func<TNode, bool> isValueNode = null) where TValue : class
	{
		var cache = new CacheProperty<TNode, TValue>(owner, valueExtractor, isValueNode);
		_cacheInvalidators.Add(() => cache.Invalidate());
		return cache;
	}

	public void RegisterInvalidatable(Action invalidateAction)
	{
		_cacheInvalidators.Add(invalidateAction);
	}

	public CachedListBuilder<TNode> CreateCachedList()
	{
		return new CachedListBuilder<TNode>(owner, this);
	}

	public CachedListBuilder<TNode> CreateCachedList(TNode fromOwner)
	{
		return new CachedListBuilder<TNode>(fromOwner, this);
	}

	public void InvalidateAll()
	{
		foreach (var invalidate in _cacheInvalidators)
		{
			invalidate();
		}
	}

	public void InvalidateAllRecursive()
	{
		InvalidateAll();

		// Cascade to all children
		foreach (var child in owner.GetChildren())
		{
			if (child is ICacheInvalidatable invalidatable)
			{
				invalidatable.InvalidateAllCaches();
			}
		}
	}
}

/// <summary>
/// Interface for nodes that support cache invalidation
/// </summary>
public interface ICacheInvalidatable
{
	void InvalidateAllCaches();
}
