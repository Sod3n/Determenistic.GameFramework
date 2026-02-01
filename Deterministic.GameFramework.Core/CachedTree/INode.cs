namespace Deterministic.GameFramework.Core;

/// <summary>
/// Interface for tree node structure, enabling hierarchical relationships
/// </summary>
public interface INode<T> where T : class, INode<T>
{
	T? NodeGetParent();
	System.Collections.Generic.IEnumerable<T> GetChildren();
}
