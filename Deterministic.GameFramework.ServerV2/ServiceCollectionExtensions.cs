using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.NetworkV2.Server;

namespace Deterministic.GameFramework.ServerV2;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDeterministicGameServer(this IServiceCollection services)
    {
        services.AddSingleton<MatchManager>();
        services.AddSingleton<MatchBroadcaster>();
        // User must provide IMatchFactory implementation
        return services;
    }
    
    public static void UseDeterministicGameServer(this IServiceProvider provider)
    {
        // Initialize Broadcaster (force instantiation)
        provider.GetRequiredService<MatchBroadcaster>();
    }
}
