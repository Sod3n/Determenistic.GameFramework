using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.Network.Server;

namespace Deterministic.GameFramework.ServerV2;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeterministicGameServer(this IServiceCollection services, Action<MatchmakingOptions>? configureOptions = null)
    {
        var options = new MatchmakingOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<MatchManager>();
        services.AddSingleton<MatchBroadcaster>();
        services.AddSingleton<IMatchmakingService, MatchmakingService>();
        // User must provide IMatchFactory implementation
        return services;
    }
    
    public static void UseDeterministicGameServer(this IServiceProvider provider)
    {
        // Initialize Broadcaster (force instantiation)
        provider.GetRequiredService<MatchBroadcaster>();
        // Initialize Matchmaking Service (force instantiation to start timer)
        provider.GetRequiredService<IMatchmakingService>();
    }
}
