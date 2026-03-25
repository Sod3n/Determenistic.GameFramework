using System;

namespace Deterministic.GameFramework.ECS;

/// <summary>
/// A system that splits its work into two phases: a prepare phase that reads
/// EntityWorld and returns an Action for heavy computation, and an apply phase
/// that writes results back. The system itself should be stateless — all per-tick
/// state is captured in the closure returned by PrepareStep.
/// </summary>
public interface IAsyncSystem : ISystem
{
    /// <summary>
    /// Read from EntityWorld and return an Action that performs heavy computation.
    /// The Action runs on a background thread and must NOT access EntityWorld.
    /// Capture any needed state in the closure. Return null to skip this tick.
    /// Runs on the main thread.
    /// </summary>
    Action? PrepareStep(EntityWorld state);

    /// <summary>
    /// Write results back to EntityWorld. Runs on the main thread after the
    /// background Action completes. Must NOT be called concurrently with other systems.
    /// </summary>
    void ApplyStep(EntityWorld state);
}
