namespace Deterministic.GameFramework.Network;

/// <summary>
/// Factory interface for creating game state instances.
/// Each game can implement this to provide custom initialization logic.
/// </summary>
public interface IGameStateFactory<TMatchData, out TGameState> where TGameState : LeafDomain
{
    /// <summary>
    /// Creates a new game state instance with the specified match data.
    /// </summary>
    /// <param name="matchData">The match data containing all necessary information</param>
    /// <returns>A new game state instance</returns>
    TGameState CreateGameState(TMatchData matchData);
}
