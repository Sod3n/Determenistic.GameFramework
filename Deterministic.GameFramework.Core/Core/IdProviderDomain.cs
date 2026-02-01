using Deterministic.GameFramework.Core.Actions;

namespace Deterministic.GameFramework.Core;

/// <summary>
/// Domain that provides counter-based ID assignment to newly added domains.
/// Add this as a subdomain to any domain that needs deterministic ID management.
/// It will automatically intercept AddSubdomainAction executions and assign IDs.
/// </summary>
public class IdProviderDomain : BranchDomain
{
    private readonly HashSet<int> _assignedIds = new();
    private readonly BranchDomain _targetDomain;
    private int _counter = 1;
    
    public Action<int> OnIdAssigned { get; set; } = _ => { };
    
    /// <summary>
    /// Current counter value. Used for desync detection.
    /// </summary>
    public int CurrentCounter => _counter;

    public IdProviderDomain(BranchDomain targetDomain) : base(targetDomain)
    {
        _targetDomain = targetDomain;
        
        // React to subdomain additions to assign counter-based IDs
        new Reaction<LeafDomain, AddSubdomainAction>(_targetDomain)
            .After((domain, action) => {
                AssignCounterBasedIdToNewDomain(action.Child);
                // Also process any existing subdomains of the newly added domain
                ProcessExistingSubdomains(action.Child);
            })
            .AddTo(Disposables);
    }

    private void AssignCounterBasedIdToNewDomain(LeafDomain domain)
    {
        // Only assign if this domain doesn't already have a counter-based ID
        if (!_assignedIds.Contains(domain.Id))
        {
            var oldId = domain.Id;
            var counterBasedId = GenerateCounterBasedId();
            domain.Id = counterBasedId;
            _assignedIds.Add(counterBasedId);
            OnIdAssigned?.Invoke(counterBasedId);
        }
    }
    
    private void ProcessExistingSubdomains(LeafDomain domain)
    {
        // Process existing subdomains recursively
        if (domain is BranchDomain domainWithSubdomains)
        {
            foreach (var subdomain in domainWithSubdomains.Subdomains.ToList())
            {
                AssignCounterBasedIdToNewDomain(subdomain);
                ProcessExistingSubdomains(subdomain);
            }
        }
    }

    private int GenerateCounterBasedId()
    {
        return _counter++;
    }

    /// <summary>
    /// Manually assign counter-based IDs to all existing domains in the target tree
    /// </summary>
    public void AssignIdsToExistingDomains()
    {
        var existingDomains = new List<LeafDomain>();
        CollectDomainsRecursively(_targetDomain, existingDomains);
        
        // Sort by type name for consistent ordering
        existingDomains.Sort((a, b) => string.Compare(a.GetType().Name, b.GetType().Name, StringComparison.Ordinal));
        
        foreach (var domain in existingDomains)
        {
            AssignCounterBasedIdToNewDomain(domain);
        }
    }

    private void CollectDomainsRecursively(LeafDomain domain, List<LeafDomain> collection)
    {
        collection.Add(domain);
        
        foreach (var child in domain.GetChildren())
        {
            CollectDomainsRecursively(child, collection);
        }
    }
}
