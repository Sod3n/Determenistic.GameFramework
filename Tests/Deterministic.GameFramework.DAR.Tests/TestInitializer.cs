using System.Runtime.CompilerServices;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR.Tests;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Force registration of all components in this assembly before any tests run
        // and before any ComponentId<T> static constructors are triggered.
        try 
        {
            ComponentId.RegisterAssembly(typeof(TestInitializer).Assembly);
            // We also need ECS World assembly for some internal types if they are used as components
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TestInitializer] Failed to register assemblies: {ex}");
            throw;
        }
    }
}
