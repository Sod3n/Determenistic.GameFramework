namespace Deterministic.GameFramework.Core;

/// <summary>
/// Shuffles a list using a deterministic seed.
/// The seed is provided by RandomProviderDomain so server and client shuffle identically.
/// </summary>
public class Shuffle<T>(ObservableAttributeList<T> target) : DARAction<LeafDomain, Shuffle<T>>, IRequireRandom
{
    public Random Random { get; set; }

    protected override void ExecuteProcess(LeafDomain domain)
    {
        target.Shuffle(Random);
    }
    
}