using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Direction to collect items from the tree
/// </summary>
public enum CacheDirection
{
	/// <summary>Collect from self up to root (child sees parent items)</summary>
	UpToRoot,
	/// <summary>Collect from self down to leaves (parent sees child items)</summary>
	DownToLeaves
}

/// <summary>
/// A cached list that stores local items and aggregates from tree on access
/// Automatically invalidates cache when modified
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class CachedList<TNode, T> : IList<T> where TNode : class, INode<TNode>
{
	[JsonProperty] private readonly List<T> _localItems = new();
	private List<T> _cachedAggregated;
	private bool _isDirty = true;
	private readonly TNode _owner;
	private readonly Func<TNode, IEnumerable<T>> _itemsExtractor;
	private readonly Action _onModified;
	private readonly CacheDirection _direction;

	public CachedList(TNode owner, Func<TNode, IEnumerable<T>> itemsExtractor, Action onModified = null, CacheDirection direction = CacheDirection.UpToRoot)
	{
		_owner = owner;
		_itemsExtractor = itemsExtractor;
		_onModified = onModified;
		_direction = direction;
	}

	public void Invalidate()
	{
		_isDirty = true;
	}

	private void EnsureValid()
	{
		if (!_isDirty)
			return;

		_cachedAggregated = new List<T>();

		// Always collect from owner first using extractor
		_cachedAggregated.AddRange(_itemsExtractor(_owner));

		// Then collect based on direction
		if (_direction == CacheDirection.UpToRoot)
		{
			// Walk up parent chain
			var parent = _owner.NodeGetParent();
			if (parent != null)
			{
				CollectFromParents(parent, _cachedAggregated);
			}
		}
		else // DownToLeaves
		{
			// Walk down children
			foreach (var child in _owner.GetChildren())
			{
				CollectFromChildren(child, _cachedAggregated);
			}
		}

		_isDirty = false;
	}

	private void CollectFromParents(TNode node, List<T> accumulator)
	{
		if (node == null)
			return;

		// Add items from this node
		accumulator.AddRange(_itemsExtractor(node));

		// Continue up the chain
		var parent = node.NodeGetParent();
		if (parent != null)
		{
			CollectFromParents(parent, accumulator);
		}
	}

	private void CollectFromChildren(TNode node, List<T> accumulator)
	{
		if (node == null)
			return;

		// Add items from this node
		accumulator.AddRange(_itemsExtractor(node));

		// Continue down to all children
		foreach (var child in node.GetChildren())
		{
			CollectFromChildren(child, accumulator);
		}
	}

	// Modification operations - invalidate cache and trigger callback
	public void Add(T item)
	{
		_localItems.Add(item);
		Invalidate();
		_onModified?.Invoke();
	}

	public void Clear()
	{
		_localItems.Clear();
		Invalidate();
		_onModified?.Invoke();
	}

	public bool Remove(T item)
	{
		var result = _localItems.Remove(item);
		if (result)
		{
			Invalidate();
			_onModified?.Invoke();
		}
		return result;
	}

	public void Insert(int index, T item)
	{
		_localItems.Insert(index, item);
		Invalidate();
		_onModified?.Invoke();
	}

	public void RemoveAt(int index)
	{
		_localItems.RemoveAt(index);
		Invalidate();
		_onModified?.Invoke();
	}

	public T this[int index]
	{
		get
		{
			EnsureValid();
			return _cachedAggregated[index];
		}
		set
		{
			_localItems[index] = value;
			Invalidate();
			_onModified?.Invoke();
		}
	}

	// Read operations - use aggregated cache
	public bool Contains(T item)
	{
		EnsureValid();
		return _cachedAggregated.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		EnsureValid();
		_cachedAggregated.CopyTo(array, arrayIndex);
	}

	public int Count
	{
		get
		{
			EnsureValid();
			return _cachedAggregated.Count;
		}
	}

	public bool IsReadOnly => false;

	public int IndexOf(T item)
	{
		EnsureValid();
		return _cachedAggregated.IndexOf(item);
	}

	public IEnumerator<T> GetEnumerator()
	{
		EnsureValid();
		return _cachedAggregated.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	// Expose local items for tree aggregation (used by itemsExtractor)
	public IReadOnlyList<T> GetLocalItems() => _localItems;
	
	// Fluent API for easier creation
	public static CachedListBuilder<TNode, T> From<TNode, T>(TNode owner) where TNode : class, INode<TNode>
	{
		return new CachedListBuilder<TNode, T>(owner);
	}
}

/// <summary>
/// Static helper class for creating CachedList with better type inference
/// </summary>
public static class CachedList
{
	public static CachedListBuilder<LeafDomain> From(LeafDomain owner)
	{
		return new CachedListBuilder<LeafDomain>(owner);
	}
	
	public static CachedListBuilder<TNode> From<TNode>(TNode owner) where TNode : class, INode<TNode>
	{
		return new CachedListBuilder<TNode>(owner);
	}
}

/// <summary>
/// Builder for creating CachedList with better type inference
/// </summary>
public class CachedListBuilder<TNode> where TNode : class, INode<TNode>
{
	private readonly TNode _owner;
	private readonly TreeCacheManager<TNode> _cacheManager;

	internal CachedListBuilder(TNode owner)
	{
		_owner = owner;
	}

	internal CachedListBuilder(TNode owner, TreeCacheManager<TNode> cacheManager)
	{
		_owner = owner;
		_cacheManager = cacheManager;
	}

	public CachedListBuilder<TNode, T> Extract<T>(Func<TNode, IEnumerable<T>> extractor)
	{
		return new CachedListBuilder<TNode, T>(_owner, _cacheManager).Extract(extractor);
	}
}

/// <summary>
/// Builder for creating CachedList with fluent API
/// </summary>
public class CachedListBuilder<TNode, T> where TNode : class, INode<TNode>
{
	private readonly TNode _owner;
	private readonly TreeCacheManager<TNode> _cacheManager;
	private Func<TNode, IEnumerable<T>> _extractor;
	private Action _onModified;
	private CacheDirection _direction = CacheDirection.UpToRoot;

	internal CachedListBuilder(TNode owner)
	{
		_owner = owner;
	}

	internal CachedListBuilder(TNode owner, TreeCacheManager<TNode> cacheManager)
	{
		_owner = owner;
		_cacheManager = cacheManager;
	}

	public CachedListBuilder<TNode, T> Extract(Func<TNode, IEnumerable<T>> extractor)
	{
		_extractor = extractor;
		return this;
	}

	public CachedListBuilder<TNode, T> OnModified(Action callback)
	{
		_onModified = callback;
		return this;
	}

	public CachedListBuilder<TNode, T> Direction(CacheDirection direction)
	{
		_direction = direction;
		return this;
	}

	public CachedListBuilder<TNode, T> UpToRoot()
	{
		_direction = CacheDirection.UpToRoot;
		return this;
	}

	public CachedListBuilder<TNode, T> DownToLeaves()
	{
		_direction = CacheDirection.DownToLeaves;
		return this;
	}

	public CachedList<TNode, T> Build()
	{
		if (_extractor == null)
			throw new InvalidOperationException("Extractor function must be specified");
		
		// Create the callback that includes cache manager invalidation if available
		Action combinedCallback = null;
		if (_cacheManager != null)
		{
			combinedCallback = () =>
			{
				_cacheManager.InvalidateAllRecursive();
				_onModified?.Invoke();
			};
		}
		else
		{
			combinedCallback = _onModified;
		}
		
		var cachedList = new CachedList<TNode, T>(_owner, _extractor, combinedCallback, _direction);
		
		// Auto-register with cache manager if available
		_cacheManager?.RegisterInvalidatable(cachedList.Invalidate);
		
		return cachedList;
	}
}
