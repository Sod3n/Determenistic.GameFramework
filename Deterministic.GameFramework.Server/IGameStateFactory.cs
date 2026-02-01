namespace Deterministic.GameFramework.Server;

/// <summary>
/// Factory interface for creating game state instances.
/// Each game can implement this to provide custom initialization logic.
/// </summary>
public interface IGameStateFactory<out TGameState> where TGameState : LeafDomain
{
    /// <summary>
    /// Creates a new game state instance with the specified match ID.
    /// </summary>
    /// <param name="matchId">The unique identifier for the match</param>
    /// <returns>A new game state instance</returns>
    TGameState CreateGameState(Guid matchId);
}
