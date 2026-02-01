using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// A cached dictionary that stores local items and aggregates from tree on access
/// Automatically invalidates cache when modified
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class CachedDictionary<TNode, TKey, TValue> : IDictionary<TKey, TValue> where TNode : class, INode<TNode>
{
	[JsonProperty] private readonly Dictionary<TKey, TValue> _localItems = new();
	private Dictionary<TKey, TValue> _cachedAggregated;
	private bool _isDirty = true;
	private readonly TNode _owner;
	private readonly Func<TNode, IEnumerable<KeyValuePair<TKey, TValue>>> _itemsExtractor;
	private readonly Action? _onModified;
	private readonly CacheDirection _direction;

	public CachedDictionary(TNode owner, Func<TNode, IEnumerable<KeyValuePair<TKey, TValue>>> itemsExtractor, Action? onModified = null, CacheDirection direction = CacheDirection.UpToRoot)
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

		_cachedAggregated = new Dictionary<TKey, TValue>();

		// Always collect from owner first using extractor
		foreach (var kvp in _itemsExtractor(_owner))
		{
			_cachedAggregated[kvp.Key] = kvp.Value;
		}

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

	private void CollectFromParents(TNode node, Dictionary<TKey, TValue> accumulator)
	{
		if (node == null)
			return;

		// Add items from this node (parent values don't override child values)
		foreach (var kvp in _itemsExtractor(node))
		{
			if (!accumulator.ContainsKey(kvp.Key))
			{
				accumulator[kvp.Key] = kvp.Value;
			}
		}

		// Continue up the chain
		var parent = node.NodeGetParent();
		if (parent != null)
		{
			CollectFromParents(parent, accumulator);
		}
	}

	private void CollectFromChildren(TNode node, Dictionary<TKey, TValue> accumulator)
	{
		if (node == null)
			return;

		// Add items from this node (child values override parent values)
		foreach (var kvp in _itemsExtractor(node))
		{
			accumulator[kvp.Key] = kvp.Value;
		}

		// Continue down to all children
		foreach (var child in node.GetChildren())
		{
			CollectFromChildren(child, accumulator);
		}
	}

	// Modification operations - invalidate cache and trigger callback
	public void Add(TKey key, TValue value)
	{
		_localItems.Add(key, value);
		Invalidate();
		_onModified?.Invoke();
	}

	public void Add(KeyValuePair<TKey, TValue> item)
	{
		_localItems.Add(item.Key, item.Value);
		Invalidate();
		_onModified?.Invoke();
	}

	public void Clear()
	{
		_localItems.Clear();
		Invalidate();
		_onModified?.Invoke();
	}

	public bool Remove(TKey key)
	{
		var result = _localItems.Remove(key);
		if (result)
		{
			Invalidate();
			_onModified?.Invoke();
		}
		return result;
	}

	public bool Remove(KeyValuePair<TKey, TValue> item)
	{
		var result = ((IDictionary<TKey, TValue>)_localItems).Remove(item);
		if (result)
		{
			Invalidate();
			_onModified?.Invoke();
		}
		return result;
	}

	public TValue this[TKey key]
	{
		get
		{
			EnsureValid();
			return _cachedAggregated[key];
		}
		set
		{
			_localItems[key] = value;
			Invalidate();
			_onModified?.Invoke();
		}
	}

	// Read operations - use aggregated cache
	public bool ContainsKey(TKey key)
	{
		EnsureValid();
		return _cachedAggregated.ContainsKey(key);
	}

	public bool Contains(KeyValuePair<TKey, TValue> item)
	{
		EnsureValid();
		return _cachedAggregated.Contains(item);
	}

	public bool TryGetValue(TKey key, out TValue value)
	{
		EnsureValid();
		return _cachedAggregated.TryGetValue(key, out value);
	}

	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
	{
		EnsureValid();
		((IDictionary<TKey, TValue>)_cachedAggregated).CopyTo(array, arrayIndex);
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

	public ICollection<TKey> Keys
	{
		get
		{
			EnsureValid();
			return _cachedAggregated.Keys;
		}
	}

	public ICollection<TValue> Values
	{
		get
		{
			EnsureValid();
			return _cachedAggregated.Values;
		}
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
	{
		EnsureValid();
		return _cachedAggregated.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	// Expose local items for tree aggregation (used by itemsExtractor)
	public IReadOnlyDictionary<TKey, TValue> GetLocalItems() => _localItems;
}
