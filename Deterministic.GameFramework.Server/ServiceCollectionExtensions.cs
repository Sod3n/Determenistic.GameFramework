using Microsoft.Extensions.DependencyInjection;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Extension methods to simplify multiplayer server setup.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all required services for a multiplayer server with a factory function.
    /// Uses DefaultGameHub and a simple delegate factory.
    /// </summary>
    public static IServiceCollection AddMultiplayerServer<TGameState>(
        this IServiceCollection services,
        Func<Guid, TGameState> gameStateFactory)
        where TGameState : NetworkGameState
    {
        services.AddSingleton<ServerDomain>();
        services.AddSingleton<IGameStateFactory<TGameState>>(
            _ => new DefaultGameStateFactory<TGameState>(gameStateFactory));
        services.AddSingleton<MatchManager<TGameState>>();
        services.AddSignalR();
        
        return services;
    }
    
    /// <summary>
    /// Adds all required services for a multiplayer server with a custom factory.
    /// Uses DefaultGameHub but allows custom game state creation logic.
    /// </summary>
    public static IServiceCollection AddMultiplayerServer<TGameState, TFactory>(this IServiceCollection services)
        where TGameState : NetworkGameState
        where TFactory : class, IGameStateFactory<TGameState>
    {
        services.AddSingleton<ServerDomain>();
        services.AddSingleton<IGameStateFactory<TGameState>, TFactory>();
        services.AddSingleton<MatchManager<TGameState>>();
        services.AddSignalR();
        
        return services;
    }
}
