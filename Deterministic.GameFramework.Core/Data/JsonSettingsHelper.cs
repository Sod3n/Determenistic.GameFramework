using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Deterministic.GameFramework.Core.Data;

public static class JsonSettingsHelper
{
    public static JsonSerializerSettings DefaultSettings => new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        Converters = { new StringEnumConverter(new SnakeCaseNamingStrategy()) }
    };
    
    public static JsonSerializerSettings CreateSettings(
        NamingStrategy? namingStrategy = null,
        params JsonConverter[] additionalConverters)
    {
        namingStrategy ??= new SnakeCaseNamingStrategy();
        
        var settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = namingStrategy
            }
        };
        
        settings.Converters.Add(new StringEnumConverter(namingStrategy));
        
        foreach (var converter in additionalConverters)
        {
            settings.Converters.Add(converter);
        }
        
        return settings;
    }
    
    public static JsonSerializerSettings CamelCaseSettings => CreateSettings(new CamelCaseNamingStrategy());
    
    public static JsonSerializerSettings PascalCaseSettings => CreateSettings(new DefaultNamingStrategy());
}
