using System;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Caches a single property value by walking up the parent chain
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class CacheProperty<TNode, TValue> where TNode : class, INode<TNode> where TValue : class
{
	private TValue _cachedValue;
	private bool _isDirty = true;
	private readonly TNode _owner;
	private readonly Func<TNode, TValue> _valueExtractor;
	private readonly Func<TNode, bool> _isValueNode;

	public CacheProperty(TNode owner, Func<TNode, TValue> valueExtractor, Func<TNode, bool> isValueNode = null)
	{
		_owner = owner;
		_valueExtractor = valueExtractor;
		_isValueNode = isValueNode ?? (node => valueExtractor(node) != null);
	}

	public TValue GetValue()
	{
		EnsureValid();
		return _cachedValue;
	}

	public void Invalidate()
	{
		_isDirty = true;
	}

	private void EnsureValid()
	{
		if (!_isDirty)
			return;

		// Check if owner node has the value
		if (_isValueNode(_owner))
		{
			_cachedValue = _valueExtractor(_owner);
		}
		else
		{
			// Walk up parent chain
			var parent = _owner.NodeGetParent();
			if (parent != null)
			{
				// Recursively get value from parent
				_cachedValue = FindValueInTree(parent);
			}
			else
			{
				_cachedValue = null;
			}
		}

		_isDirty = false;
	}

	private TValue FindValueInTree(TNode node)
	{
		if (node == null)
			return null;

		if (_isValueNode(node))
			return _valueExtractor(node);

		var parent = node.NodeGetParent();
		return parent != null ? FindValueInTree(parent) : null;
	}
}
