using Deterministic.GameFramework.Physics2D.Systems;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

public class RapierPhysicsStateTests
{
    [Fact]
    public void Dispose_ClearsAllState()
    {
        var state = new RapierPhysicsState();

        // Add some dummy data
        state.EntityToBody[1] = 100;
        state.BodyToEntity[100] = 1;

        // We can't easily mock RapierWorld or Processors to verify their Dispose/Clear without more access,
        // but we can verify the collections.

        state.Dispose();

        state.World.Should().BeNull();
        state.EntityToBody.Should().BeEmpty();
        state.BodyToEntity.Should().BeEmpty();
    }
}
