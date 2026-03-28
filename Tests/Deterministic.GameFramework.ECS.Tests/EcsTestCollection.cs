using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[CollectionDefinition("ECS Tests", DisableParallelization = true)]
public class EcsTestCollection : ICollectionFixture<object>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
