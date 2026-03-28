using System.Diagnostics;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Profiler;

/// <summary>
/// Decorator that wraps an ISystem and records its execution time each tick.
/// </summary>
public sealed class ProfiledSystem : ISystem
{
    private readonly ISystem _inner;
    private readonly Stopwatch _sw = new();

    /// <summary>The original unwrapped system.</summary>
    public ISystem Inner => _inner;

    /// <summary>Name of the wrapped system (its type name).</summary>
    public string Name { get; }

    /// <summary>Elapsed time of the last Update call in seconds.</summary>
    public double LastElapsedSeconds { get; private set; }

    /// <summary>Rolling statistics for this system's execution time.</summary>
    public RollingStats Stats { get; }

    internal ProfiledSystem(ISystem inner, int windowSize)
    {
        _inner = inner;
        Name = inner.GetType().Name;
        Stats = new RollingStats(windowSize);
    }

    public void Update(EntityWorld state)
    {
        _sw.Restart();
        _inner.Update(state);
        _sw.Stop();

        LastElapsedSeconds = _sw.Elapsed.TotalSeconds;
        Stats.Record(LastElapsedSeconds);
    }
}
