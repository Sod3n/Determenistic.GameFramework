using Deterministic.GameFramework.CoreV2;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class DeterministicRandomTests
    {
        [Fact]
        public void Random_ShouldBeDeterministic_WithSameSeed()
        {
            var rng1 = new DeterministicRandom(12345);
            var rng2 = new DeterministicRandom(12345);

            for (int i = 0; i < 100; i++)
            {
                rng1.Next().Should().Be(rng2.Next(), $"Sequence should match at index {i}");
            }
        }

        [Fact]
        public void Random_ShouldDiverge_WithDifferentSeeds()
        {
            var rng1 = new DeterministicRandom(12345);
            var rng2 = new DeterministicRandom(67890);

            // It is extremely unlikely that 100 random numbers will match exactly
            bool allMatched = true;
            for (int i = 0; i < 100; i++)
            {
                if (rng1.Next() != rng2.Next())
                {
                    allMatched = false;
                    break;
                }
            }

            allMatched.Should().BeFalse("Different seeds should produce different sequences");
        }
    }
}
