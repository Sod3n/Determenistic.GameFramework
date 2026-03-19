using System;
using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StableIdMismatchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticIdMismatch = "DGF102";
        public const string DiagnosticIdDeleted = "DGF103";

        private static readonly DiagnosticDescriptor RuleMismatch = new DiagnosticDescriptor(
            DiagnosticIdMismatch, 
            "StableId Mismatch", 
            "StableId '{0}' is mapped to '{1}' in StableIds.json, but used by '{2}' here. Did you rename the class? Please update StableIds.json or revert the ID.", 
            "Design", 
            DiagnosticSeverity.Error, 
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(RuleMismatch);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var additionalFiles = compilationContext.Options.AdditionalFiles;
                var StableIdsFile = additionalFiles.FirstOrDefault(file => file.Path.EndsWith("StableIds.json", StringComparison.OrdinalIgnoreCase));

                var idToNameMap = new Dictionary<int, string>();

                if (StableIdsFile != null)
                {
                    var sourceText = StableIdsFile.GetText(compilationContext.CancellationToken);
                    if (sourceText != null)
                    {
                        var lines = sourceText.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            // Very simple manual JSON parse for flat dictionary
                            // "Namespace.TypeName": 123
                            var parts = line.Split(':');
                            if (parts.Length == 2)
                            {
                                var keyPart = parts[0].Trim();
                                var valPart = parts[1].Trim().TrimEnd(',');

                                if (keyPart.StartsWith("\"") && keyPart.EndsWith("\""))
                                {
                                    var key = keyPart.Substring(1, keyPart.Length - 2);
                                    if (int.TryParse(valPart, out int id))
                                    {
                                        idToNameMap[id] = key;
                                    }
                                }
                            }
                        }
                    }
                }

                compilationContext.RegisterSymbolAction(symbolContext =>
                {
                    if (idToNameMap.Count == 0) return;

                    var namedTypeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                    var attributes = namedTypeSymbol.GetAttributes();
                    var StableIdAttr = attributes.FirstOrDefault(attr => 
                        attr.AttributeClass != null && 
                        (attr.AttributeClass.Name == "StableIdAttribute" || attr.AttributeClass.Name == "StableId"));

                    if (StableIdAttr == null || StableIdAttr.ConstructorArguments.Length == 0)
                        return;

                    if (StableIdAttr.ConstructorArguments[0].Value is int id)
                    {
                        var currentFullName = namedTypeSymbol.ToDisplayString();

                        if (idToNameMap.TryGetValue(id, out var registeredName))
                        {
                            if (registeredName != currentFullName)
                            {
                                var diagnostic = Diagnostic.Create(RuleMismatch, namedTypeSymbol.Locations[0], id, registeredName, currentFullName);
                                symbolContext.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }, SymbolKind.NamedType);
            });
        }
    }
}
