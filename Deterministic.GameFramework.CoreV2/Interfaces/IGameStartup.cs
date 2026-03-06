using System;

namespace Deterministic.GameFramework.CoreV2;

/// <summary>
/// Interface for defining logic that should run when the GameLoop is initialized.
/// Implement this to perform world setup, entity spawning, or other initialization tasks.
/// </summary>
public interface IGameStartup
{
    void Configure(GameLoop gameLoop);
}
