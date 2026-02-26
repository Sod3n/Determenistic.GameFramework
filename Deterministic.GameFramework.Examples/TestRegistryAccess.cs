using System;
using Deterministic.GameFramework.Generated.DeterministicGameFrameworkExamples;

namespace Deterministic.GameFramework.Examples
{
    public static class TestRegistryAccess
    {
        public static void Test()
        {
            var dict = NetworkIdRegistry.TypeToId;
            Console.WriteLine($"Registered network types: {dict.Count}");
        }
    }
}
