namespace Deterministic.GameFramework.Core.Domain;

/// <summary>
/// Maintains an ID → Domain lookup for the entire tree.
/// Enables GetDomain(id) lookups for network action targeting.
/// </summary>
public class DomainRegistry : BranchDomain
{
    private readonly CachedDictionary<LeafDomain, int, LeafDomain> _registry;
    private readonly BranchDomain _root;
    
    public DomainRegistry(BranchDomain root) : base(root)
    {
        _root = root;
        _registry = new CachedDictionary<LeafDomain, int, LeafDomain>(
            root,
            node => new[] { new KeyValuePair<int, LeafDomain>(node.Id, node) },
            null,
            CacheDirection.DownToLeaves
        );
        
        // Invalidate when tree changes
        CacheManager.RegisterInvalidatable(() => _registry.Invalidate());
    }
    
    public LeafDomain? GetDomain(int id)
    {
        // ID 0 means root GameState (before IdProvider assigns IDs)
        if (id == 0)
            return _root;
            
        return _registry.TryGetValue(id, out var domain) ? domain : null;
    }
    
    public bool TryGetDomain(int id, out LeafDomain domain)
    {
        if (id == 0)
        {
            domain = _root;
            return true;
        }
        return _registry.TryGetValue(id, out domain);
    }
}
