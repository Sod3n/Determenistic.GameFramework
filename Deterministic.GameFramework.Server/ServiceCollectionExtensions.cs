using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    public static IServiceCollection AddMultiplayerServer<TMatchData, TGameState>(
        this IServiceCollection services,
        Func<TMatchData, TGameState> gameStateFactory)
        where TGameState : NetworkGameState
    {
        services.AddSingleton<ServerDomain>();
        services.AddSingleton<IGameStateFactory<TMatchData, TGameState>>(
            _ => new DefaultGameStateFactory<TMatchData, TGameState>(gameStateFactory));
        services.AddSingleton<MatchManager<TMatchData, TGameState>>();
        services.AddSignalR()
            .AddNewtonsoftJsonProtocol();
        
        return services;
    }
    
    /// <summary>
    /// Adds all required services for a multiplayer server with a custom factory.
    /// Uses DefaultGameHub but allows custom game state creation logic.
    /// </summary>
    public static IServiceCollection AddMultiplayerServer<TMatchData, TGameState, TFactory>(this IServiceCollection services)
        where TGameState : NetworkGameState
        where TFactory : class, IGameStateFactory<TMatchData, TGameState>
    {
        services.AddSingleton<ServerDomain>();
        services.AddSingleton<IGameStateFactory<TMatchData, TGameState>, TFactory>();
        services.AddSingleton<MatchManager<TMatchData, TGameState>>();
        services.AddSignalR()
            .AddNewtonsoftJsonProtocol();
        
        return services;
    }
}
