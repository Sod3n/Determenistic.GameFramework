using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Actions;
using Deterministic.GameFramework.Core.Extensions;
using Deterministic.GameFramework.Core.Utils;


/// <summary>
/// Domain class that can have both parent and children.
/// Use this for composite domains that need to manage subdomains.
/// </summary>
public abstract class BranchDomain : LeafDomain
{
	public readonly ObservableAttributeList<LeafDomain> Subdomains = new();
	
	public BranchDomain(BranchDomain? parent) : base(parent)
	{
		
		// React to subdomain additions
		Subdomains.ObserveAdd(this, e =>
		{
			e.Item._parentDomain = this;
			CacheManager?.InvalidateAllRecursive();
			new AddSubdomainAction(this, e.Item).Execute(this);
		});
		
		// React to subdomain removals
		Subdomains.ObserveRemove(this, e =>
		{
			e.Item._parentDomain = null;
			CacheManager?.InvalidateAllRecursive();
			new RemoveSubdomainAction(this, e.Item).Execute(this);
		});
		
		// React to list clear - clear parent references before
		Subdomains.ObserveBeforeClear(this, () =>
		{
			foreach (var subdomain in Subdomains)
				subdomain._parentDomain = null;
		});
		
		// Invalidate caches after clear
		Subdomains.ObserveClear(this, () =>
		{
			CacheManager?.InvalidateAllRecursive();
		});
	}
	
	// Override to return children
	public override IEnumerable<LeafDomain> GetChildren() => Subdomains;

	// ---- Domain Management ---- //

	// Simple access to data should always be direct without any modifications from reactions
	public LeafDomain GetSubdomain(int index)
	{
		return Subdomains[index];
	}

	public bool HasSubdomain(LeafDomain domain)
	{
		return Subdomains.Contains(domain);
	}

	public override T? GetFirst<T>(bool includeSelf = false) where T : class
	{
		var subdomains = Subdomains;

		foreach (var subdomain in subdomains)
		{
			if (subdomain is T typedDomain)
				return typedDomain;
		}

		if (includeSelf && this is T selfTyped)
			return selfTyped;

		// Recursive search
		foreach (var subdomain in subdomains)
		{
			if (subdomain is BranchDomain domainSubdomain)
			{
				var result = domainSubdomain.GetFirst<T>();
				if (result != null)
					return result;
			}
		}

		return null;
	}

	public object? GetFirst(Type type, bool includeSelf = false, bool recursive = true)
	{
		var subdomains = Subdomains;

		foreach (var subdomain in subdomains)
		{
			if (type.IsInstanceOfType(subdomain))
				return subdomain;
		}

		if (includeSelf && type.IsInstanceOfType(this))
			return this;

		if (!recursive)
			return null;

		// Recursive search
		foreach (var subdomain in subdomains)
		{
			if (subdomain is BranchDomain domainSubdomain)
			{
				var result = domainSubdomain.GetFirst(type, false, true);
				if (result != null)
					return result;
			}
		}

		return null;
	}

	public override List<T> GetAll<T>(bool includeSelf = false, bool recursive = true) where T : class
	{
		var subdomains = Subdomains.ToList();
		var result = new List<T>();

		foreach (var subdomain in subdomains)
		{
			if (subdomain is T typedDomain)
				result.Add(typedDomain);
		}

		if (includeSelf && this is T selfTyped)
			result.Add(selfTyped);

		if (!recursive)
			return result;

		foreach (var subdomain in subdomains)
		{
			if (subdomain is BranchDomain domainSubdomain)
			{
				var subResults = domainSubdomain.GetAll<T>();
				result.AddRange(subResults);
			}
		}

		return result;
	}

	public List<object> GetAll(Type type, bool includeSelf = false, bool recursive = true)
	{
		var subdomains = Subdomains.ToList();
		var result = new List<object>();

		foreach (var subdomain in subdomains)
		{
			if (type.IsInstanceOfType(subdomain))
				result.Add(subdomain);
		}

		if (includeSelf && type.IsInstanceOfType(this))
			result.Add(this);

		if (!recursive)
			return result;

		foreach (var subdomain in subdomains)
		{
			if (subdomain is BranchDomain domainSubdomain)
			{
				var subResults = domainSubdomain.GetAll(type, false, true);
				result.AddRange(subResults);
			}
		}

		return result;
	}

	public bool IsEmpty()
	{
		return Subdomains.Count == 0;
	}

	public override void Dispose()
	{
		// Snapshot to avoid collection modified exception
		var snapshot = Subdomains.ToList();
		foreach (var subdomain in snapshot)
		{
			subdomain.Dispose();
		}

		base.Dispose();
	}
}