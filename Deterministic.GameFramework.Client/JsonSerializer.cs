using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Deterministic.GameFramework.Client
{
    public static class JsonSerializer
    {
        internal sealed class DotNetCompatibleSerializationBinder : DefaultSerializationBinder
        {
            private const string CoreLibAssembly = "System.Private.CoreLib";
            private const string MscorlibAssembly = "mscorlib";

            public override Type BindToType(string assemblyName, string typeName)
            {
                if (assemblyName == CoreLibAssembly)
                {
                    assemblyName = MscorlibAssembly;
                    typeName = typeName.Replace(CoreLibAssembly, MscorlibAssembly);
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
            PreserveReferencesHandling = PreserveReferencesHandling.All,
            ObjectCreationHandling = ObjectCreationHandling.Auto,
            ReferenceLoopHandling = ReferenceLoopHandling.Serialize,
            SerializationBinder = new DotNetCompatibleSerializationBinder()
        };

        public static string ToJson(object? obj) => JsonConvert.SerializeObject(obj, Formatting.Indented, _settings);
        public static T? FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json, _settings);
    }
}
