namespace Deterministic.GameFramework.Core;

/// <summary>
/// Domain that provides deterministic random number generation.
/// Add this as a subdomain to any domain that needs random number generation.
/// It will automatically inject the Random instance into IRequireRandom actions.
/// </summary>
public class RandomProviderDomain : BranchDomain
{
    public Random Random { get; private set; }
    public int Seed { get; private set; }
    
    public RandomProviderDomain(BranchDomain targetDomain, int? initialSeed = null) : base(targetDomain)
    {
        var seed = initialSeed ?? System.Random.Shared.Next();
        Random = new Random(seed);
        Seed = seed;
        
        new ProvideRandomReaction(this, targetDomain).AddTo(Disposables);
    }
    
    public void Reset(int seed)
    {
        Random = new Random(seed);
    }
    
    private class ProvideRandomReaction : Reaction, IBeforeReaction<LeafDomain, IRequireRandom>
    {
        private readonly RandomProviderDomain _provider;
        
        public ProvideRandomReaction(RandomProviderDomain provider, LeafDomain target) : base(target)
        {
            _provider = provider;
        }
        
        public void OnBefore(LeafDomain domain, IRequireRandom action)
        {
            action.Random = _provider.Random;
        }
    }
}
