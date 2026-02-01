namespace Deterministic.GameFramework.Core;

/// <summary>
/// Interface for actions that require random number generation.
/// RandomProviderDomain automatically injects a seeded Random instance before execution.
/// </summary>
public interface IRequireRandom : IDARAction
{
    /// <summary>
    /// Seeded Random instance, injected by RandomProviderDomain before action executes.
    /// </summary>
    public Random Random { get; set; }
}