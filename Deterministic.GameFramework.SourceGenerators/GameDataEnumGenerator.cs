using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Deterministic.GameFramework.SourceGenerators;

[Generator]
public class GameDataEnumGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find enumgen.config.json file
        var configFile = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith("enumgen.config.json"))
            .Select(static (file, _) => file);

        // Get all JSON files
        var jsonFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".json") && !file.Path.EndsWith("enumgen.config.json"));

        // Combine config and JSON files
        var combined = configFile.Combine(jsonFiles.Collect());

        context.RegisterSourceOutput(combined, static (spc, source) => Execute(spc, source.Left, source.Right));
    }

    private static void Execute(SourceProductionContext context, AdditionalText? configFile, ImmutableArray<AdditionalText> jsonFiles)
    {
        if (configFile == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GDEG001",
                    "Config file not found",
                    "Could not find enumgen.config.json file",
                    "GameDataEnumGenerator",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
            return;
        }

        try
        {
            // Read config file
            var configContent = configFile.GetText()?.ToString();
            if (string.IsNullOrEmpty(configContent))
                return;

            // Parse config (simple regex-based parsing)
            var enumConfigs = ParseEnumConfig(configContent);

            // Generate each enum
            foreach (var enumConfig in enumConfigs)
            {
                GenerateEnum(context, enumConfig, jsonFiles);
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GDEG999",
                    "Generator error",
                    $"Error in GameData enum generator: {ex.Message}",
                    "GameDataEnumGenerator",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
        }
    }

    private static List<EnumConfig> ParseEnumConfig(string configContent)
    {
        var configs = new List<EnumConfig>();
        
        // Simple regex to parse: "EnumName": { "source": "SourceFile", "field": "field_name" }
        var pattern = @"""(\w+)""\s*:\s*\{\s*""source""\s*:\s*""([^""]+)""\s*,\s*""field""\s*:\s*""([^""]+)""\s*\}";
        var matches = Regex.Matches(configContent, pattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 4)
            {
                configs.Add(new EnumConfig(
                    match.Groups[1].Value,  // EnumName
                    match.Groups[2].Value,  // Source
                    match.Groups[3].Value   // Field
                ));
            }
        }
        
        return configs;
    }

    private static void GenerateEnum(SourceProductionContext context, EnumConfig enumConfig, ImmutableArray<AdditionalText> jsonFiles)
    {
        // Find the JSON file
        var jsonFile = FindJsonFile(jsonFiles, enumConfig.Source);
        
        if (jsonFile == null)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GDEG002",
                    "JSON file not found",
                    $"Could not find JSON file for source '{enumConfig.Source}' (enum '{enumConfig.EnumName}')",
                    "GameDataEnumGenerator",
                    DiagnosticSeverity.Error,
                    true),
                Location.None));
            return;
        }

        // Read and parse JSON
        var jsonContent = jsonFile.GetText()?.ToString();
        if (string.IsNullOrEmpty(jsonContent))
            return;

        // Extract unique values
        var uniqueValues = ExtractUniqueValues(jsonContent, enumConfig.Field);
        
        if (!uniqueValues.Any())
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "GDEG003",
                    "No values found",
                    $"No values found for field '{enumConfig.Field}' in source '{enumConfig.Source}' (enum '{enumConfig.EnumName}')",
                    "GameDataEnumGenerator",
                    DiagnosticSeverity.Warning,
                    true),
                Location.None));
            return;
        }

        // Generate enum source
        var source = GenerateEnumSource(enumConfig, uniqueValues);
        context.AddSource($"{enumConfig.EnumName}.g.cs", source);
    }

    private static AdditionalText? FindJsonFile(ImmutableArray<AdditionalText> jsonFiles, string sourceName)
    {
        return jsonFiles.FirstOrDefault(f => 
        {
            var fileName = Path.GetFileNameWithoutExtension(f.Path);
            var fullPath = f.Path;
            
            // Check direct match: Cards.json
            if (fileName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Check subfolder pattern: Cards/Cards.json
            if (fullPath.Contains($"/{sourceName}/") && fileName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                return true;
                
            // Check subfolder with different name: Cards/Definitions.json
            if (fullPath.Contains($"/{sourceName.Replace("Definition", "")}/") && fileName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                return true;
                
            return false;
        });
    }


    private static List<string> ExtractUniqueValues(string jsonContent, string fieldName)
    {
        var values = new HashSet<string>();
        
        // Use regex to find all instances of "fieldName": "value"
        var pattern = $"\"{Regex.Escape(fieldName)}\"\\s*:\\s*\"([^\"]+)\"";
        var matches = Regex.Matches(jsonContent, pattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                var value = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }
        }

        return values.OrderBy(v => v).ToList();
    }

    private static string GenerateEnumSource(EnumConfig enumConfig, List<string> values)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine($"// Generated from {enumConfig.Source}.json, field: {enumConfig.Field}");
        sb.AppendLine();
        sb.AppendLine("namespace Deterministic.GameFramework.Generated;");
        sb.AppendLine();
        sb.AppendLine($"public enum {enumConfig.EnumName}");
        sb.AppendLine("{");
        
        for (int i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var enumMember = SanitizeEnumMember(value);
            
            sb.Append($"    {enumMember}");
            if (i < values.Count - 1)
                sb.Append(",");
            sb.AppendLine();
        }
        
        sb.AppendLine("}");
        
        return sb.ToString();
    }

    private static string SanitizeEnumMember(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "_Unknown";

        // Convert snake_case to PascalCase
        var parts = value.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();
        
        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                // Capitalize first letter, lowercase the rest
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part.Substring(1).ToLowerInvariant());
                }
            }
        }
        
        var finalResult = result.ToString();
        
        // Ensure it starts with letter or underscore
        if (finalResult.Length > 0 && char.IsDigit(finalResult[0]))
        {
            finalResult = "_" + finalResult;
        }
        
        return string.IsNullOrEmpty(finalResult) ? "_Unknown" : finalResult;
    }

    private class EnumConfig
    {
        public string EnumName { get; }
        public string Source { get; }
        public string Field { get; }

        public EnumConfig(string enumName, string source, string field)
        {
            EnumName = enumName;
            Source = source;
            Field = field;
        }
    }
}
