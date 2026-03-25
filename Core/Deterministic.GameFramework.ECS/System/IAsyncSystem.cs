namespace Deterministic.GameFramework.ECS;

/// <summary>
/// A system that splits its work into three phases, allowing the heavy computation
/// to run on a background thread while other systems execute on the main thread.
/// </summary>
public interface IAsyncSystem : ISystem
{
    /// <summary>
    /// Read from EntityWorld and prepare internal state. Runs on the main thread.
    /// Must NOT be called concurrently with other systems.
    /// </summary>
    void SyncFrom(EntityWorld state);

    /// <summary>
    /// Perform heavy computation with NO EntityWorld access. Runs on a background thread.
    /// </summary>
    void Step();

    /// <summary>
    /// Write results back to EntityWorld. Runs on the main thread after Step() completes.
    /// Must NOT be called concurrently with other systems.
    /// </summary>
    void SyncTo(EntityWorld state);
}
