using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Deterministic.GameFramework.Core.Utils;

public static class JsonSerializer
{
    /// <summary>
    /// Security binder that whitelists allowed types for deserialization.
    /// Prevents malicious clients from deserializing arbitrary types.
    /// </summary>
    internal sealed class SafeSerializationBinder : DefaultSerializationBinder
    {
        private const string CoreLibAssembly = "System.Private.CoreLib";
        private const string MscorlibAssembly = "mscorlib";
        
        // Whitelist of allowed namespaces for deserialization
        private static readonly HashSet<string> AllowedNamespaces = new()
        {
            "Deterministic.GameFramework.Core",
            "Deterministic.GameFramework.Server",
            "Deterministic.GameFramework.Client",
            "Deterministic.GameFramework.Examples",
            "MultiplayerHelloWorld.Shared",
            "System.Collections.Generic",
            "System"
        };
        
        // Blacklist of dangerous types that should never be deserialized
        private static readonly HashSet<string> DangerousTypes = new()
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
            
            // Check for dangerous types
            if (DangerousTypes.Any(dangerous => typeName.StartsWith(dangerous)))
            {
                throw new JsonSerializationException(
                    $"Deserialization of type '{typeName}' is not allowed for security reasons.");
            }
            
            // Check if type is in allowed namespaces
            var isAllowed = AllowedNamespaces.Any(ns => typeName.StartsWith(ns));
            if (!isAllowed)
            {
                throw new JsonSerializationException(
                    $"Deserialization of type '{typeName}' is not allowed. " +
                    $"Only types from whitelisted namespaces can be deserialized.");
            }

            return base.BindToType(assemblyName, typeName);
        }
    }

    private static JsonSerializerSettings _settings = new()
    {
        DefaultValueHandling = DefaultValueHandling.Ignore,
        NullValueHandling = NullValueHandling.Ignore,
        TypeNameHandling = TypeNameHandling.All,
        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
        ObjectCreationHandling = ObjectCreationHandling.Auto,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Ignore loops instead of trying to serialize them
        MaxDepth = 32, // Limit serialization depth to prevent stack overflow
        SerializationBinder = new SafeSerializationBinder() // Security: whitelist allowed types
    };

    public static string ToJson(object? obj) => JsonConvert.SerializeObject(obj, Formatting.Indented, _settings);
    public static T? FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json, _settings);
}
