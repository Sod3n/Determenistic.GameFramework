using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Deterministic.GameFramework.Core.Utils;

public enum SerializationMode
{
    /// <summary>
    /// Optimized for bandwidth - minimal JSON size, no whitespace.
    /// Best for network transmission.
    /// </summary>
    Optimized,
    
    /// <summary>
    /// Informative mode - readable formatting, full type information.
    /// Best for debugging and logging.
    /// </summary>
    Informative
}

public static class JsonSerializer
{
    /// <summary>
    /// Security binder that can optionally whitelist allowed types for deserialization.
    /// By default, only blocks dangerous types. Whitelist enforcement is opt-in.
    /// </summary>
    public sealed class SafeSerializationBinder : DefaultSerializationBinder
    {
        private const string CoreLibAssembly = "System.Private.CoreLib";
        private const string MscorlibAssembly = "mscorlib";
        
        private readonly HashSet<string>? _allowedNamespaces;
        private readonly HashSet<string> _dangerousTypes;
        private readonly bool _whitelistEnabled;
        
        /// <summary>
        /// Creates a new SafeSerializationBinder.
        /// </summary>
        /// <param name="allowedNamespaces">Namespaces that are allowed for deserialization. If null, whitelist is disabled.</param>
        /// <param name="dangerousTypes">Types that should never be deserialized. If null, uses default blacklist.</param>
        public SafeSerializationBinder(
            IEnumerable<string>? allowedNamespaces = null,
            IEnumerable<string>? dangerousTypes = null)
        {
            _whitelistEnabled = allowedNamespaces != null;
            _allowedNamespaces = allowedNamespaces != null 
                ? new HashSet<string>(allowedNamespaces) 
                : null;
            
            _dangerousTypes = dangerousTypes != null 
                ? new HashSet<string>(dangerousTypes) 
                : GetDefaultDangerousTypes();
        }
        
        /// <summary>
        /// Gets a recommended whitelist of framework namespaces.
        /// Use this as a starting point when enabling whitelist mode.
        /// </summary>
        public static HashSet<string> GetRecommendedAllowedNamespaces() => new()
        {
            "Deterministic.GameFramework.Core",
            "Deterministic.GameFramework.Server",
            "Deterministic.GameFramework.Client",
            "System.Collections.Generic",
            "System"
        };
        
        /// <summary>
        /// Gets the default blacklist of dangerous types.
        /// </summary>
        public static HashSet<string> GetDefaultDangerousTypes() => new()
        {
            "System.IO.File",
            "System.IO.FileInfo",
            "System.IO.Directory",
            "System.Diagnostics.Process",
            "System.Reflection.Assembly",
            "System.Runtime.Serialization.Formatters"
        };

        public override Type BindToType(string assemblyName, string typeName)
        {
            // Handle .NET Core / .NET Framework compatibility
            if (assemblyName == CoreLibAssembly)
            {
                assemblyName = MscorlibAssembly;
                typeName = typeName.Replace(CoreLibAssembly, MscorlibAssembly);
            }
            
            // Always check for dangerous types
            if (_dangerousTypes.Any(dangerous => typeName.StartsWith(dangerous)))
            {
                throw new JsonSerializationException(
                    $"Deserialization of type '{typeName}' is not allowed for security reasons.");
            }
            
            // Only check whitelist if it's enabled
            if (_whitelistEnabled)
            {
                var isAllowed = _allowedNamespaces!.Any(ns => typeName.StartsWith(ns));
                if (!isAllowed)
                {
                    throw new JsonSerializationException(
                        $"Deserialization of type '{typeName}' is not allowed. " +
                        $"Only types from whitelisted namespaces can be deserialized.");
                }
            }

            return base.BindToType(assemblyName, typeName);
        }
    }

    private static SerializationMode _mode = SerializationMode.Optimized;
    private static JsonSerializerSettings _optimizedSettings = CreateOptimizedSettings();
    private static JsonSerializerSettings _informativeSettings = CreateInformativeSettings();
    private static JsonSerializerSettings _settings = _optimizedSettings;

    /// <summary>
    /// Creates optimized settings for minimal bandwidth usage.
    /// - No formatting (compact JSON)
    /// - Auto type handling (only when needed)
    /// - No reference preservation (smaller payload)
    /// - Ignores nulls and defaults
    /// </summary>
    private static JsonSerializerSettings CreateOptimizedSettings() => new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.Auto, // Only include $type when needed
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        PreserveReferencesHandling = PreserveReferencesHandling.None, // No $id/$ref overhead
        ObjectCreationHandling = ObjectCreationHandling.Auto,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        MaxDepth = 32,
        SerializationBinder = new SafeSerializationBinder()
    };

    /// <summary>
    /// Creates informative settings for debugging and readability.
    /// - Indented formatting (human-readable)
    /// - Full type information
    /// - Reference preservation for complex object graphs
    /// - Includes nulls for clarity
    /// </summary>
    private static JsonSerializerSettings CreateInformativeSettings() => new()
    {
        DefaultValueHandling = DefaultValueHandling.Include,
        NullValueHandling = NullValueHandling.Include,
        TypeNameHandling = TypeNameHandling.All, // Always include type info
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects, // Track references
        ObjectCreationHandling = ObjectCreationHandling.Auto,
        ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
        MaxDepth = 32,
        SerializationBinder = new SafeSerializationBinder()
    };

    /// <summary>
    /// Sets the serialization mode.
    /// </summary>
    /// <param name="mode">Optimized for bandwidth or Informative for debugging.</param>
    public static void SetMode(SerializationMode mode)
    {
        _mode = mode;
        _settings = mode == SerializationMode.Optimized ? _optimizedSettings : _informativeSettings;
    }

    /// <summary>
    /// Gets the current serialization mode.
    /// </summary>
    public static SerializationMode GetMode() => _mode;

    /// <summary>
    /// Enables whitelist mode with the specified allowed namespaces.
    /// By default, whitelist is disabled and only dangerous types are blocked.
    /// Call this at application startup if you need strict type whitelisting.
    /// </summary>
    /// <param name="allowedNamespaces">Namespaces to allow for deserialization. Use GetRecommendedAllowedNamespaces() as a starting point.</param>
    /// <param name="additionalDangerousTypes">Additional types to blacklist beyond the defaults.</param>
    public static void EnableWhitelist(
        IEnumerable<string> allowedNamespaces,
        IEnumerable<string>? additionalDangerousTypes = null)
    {
        var dangerous = SafeSerializationBinder.GetDefaultDangerousTypes();
        
        if (additionalDangerousTypes != null)
        {
            foreach (var type in additionalDangerousTypes)
            {
                dangerous.Add(type);
            }
        }
        
        var binder = new SafeSerializationBinder(allowedNamespaces, dangerous);
        _optimizedSettings.SerializationBinder = binder;
        _informativeSettings.SerializationBinder = binder;
        _settings = _mode == SerializationMode.Optimized ? _optimizedSettings : _informativeSettings;
    }

    /// <summary>
    /// Configures additional dangerous types to block (beyond defaults).
    /// Whitelist remains disabled unless EnableWhitelist() is called.
    /// </summary>
    /// <param name="additionalDangerousTypes">Additional types to blacklist.</param>
    public static void ConfigureDangerousTypes(IEnumerable<string> additionalDangerousTypes)
    {
        var dangerous = SafeSerializationBinder.GetDefaultDangerousTypes();
        foreach (var type in additionalDangerousTypes)
        {
            dangerous.Add(type);
        }
        
        var binder = new SafeSerializationBinder(null, dangerous);
        _optimizedSettings.SerializationBinder = binder;
        _informativeSettings.SerializationBinder = binder;
        _settings = _mode == SerializationMode.Optimized ? _optimizedSettings : _informativeSettings;
    }

    /// <summary>
    /// Configures the JsonSerializer with completely custom settings.
    /// Use this for full control over serialization behavior.
    /// Warning: This bypasses all default security measures and mode settings.
    /// </summary>
    /// <param name="settings">Custom JsonSerializerSettings to use.</param>
    public static void ConfigureCustomSettings(JsonSerializerSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Serializes an object to JSON using the current mode.
    /// </summary>
    public static string ToJson(object? obj)
    {
        var formatting = _mode == SerializationMode.Informative ? Formatting.Indented : Formatting.None;
        return JsonConvert.SerializeObject(obj, formatting, _settings);
    }

    /// <summary>
    /// Deserializes JSON to an object using the current mode.
    /// </summary>
    public static T? FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json, _settings);
    
    /// <summary>
    /// Serializes an object to JSON with explicit mode override.
    /// </summary>
    public static string ToJson(object? obj, SerializationMode mode)
    {
        var settings = mode == SerializationMode.Optimized ? _optimizedSettings : _informativeSettings;
        var formatting = mode == SerializationMode.Informative ? Formatting.Indented : Formatting.None;
        return JsonConvert.SerializeObject(obj, formatting, settings);
    }
    
    /// <summary>
    /// Deserializes JSON with explicit mode override.
    /// </summary>
    public static T? FromJson<T>(string json, SerializationMode mode)
    {
        var settings = mode == SerializationMode.Optimized ? _optimizedSettings : _informativeSettings;
        return JsonConvert.DeserializeObject<T>(json, settings);
    }
}
