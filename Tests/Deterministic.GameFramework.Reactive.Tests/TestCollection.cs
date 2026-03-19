using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [CollectionDefinition("Non-Parallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinition
    {
    }
}
