using Deterministic.GameFramework.Core.Utils;

namespace MultiplayerHelloWorld.Server;

/// <summary>
/// Optional configuration for JsonSerializer security.
/// By default, JsonSerializer only blocks dangerous types (like System.IO.File).
/// Enable whitelist mode only if you need strict type restrictions.
/// </summary>
public static class JsonSerializerConfig
{
    /// <summary>
    /// Enables strict whitelist mode (optional - only use if needed for security).
    /// When enabled, only types from specified namespaces can be deserialized.
    /// </summary>
    public static void EnableStrictWhitelist()
    {
        var allowedNamespaces = JsonSerializer.SafeSerializationBinder.GetRecommendedAllowedNamespaces();
        allowedNamespaces.Add("MultiplayerHelloWorld.Shared");
        
        JsonSerializer.EnableWhitelist(allowedNamespaces);
    }
    
    /// <summary>
    /// Adds additional dangerous types to block (optional).
    /// Use this if you want to block specific types beyond the defaults.
    /// </summary>
    public static void ConfigureAdditionalBlacklist()
    {
        JsonSerializer.ConfigureDangerousTypes(new[]
        {
            "SomeOther.Dangerous.Type"
        });
    }
}
