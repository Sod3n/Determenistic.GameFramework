using System;
using System.Collections.Immutable;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NetworkIdMismatchAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticIdMismatch = "DGF102";
        public const string DiagnosticIdDeleted = "DGF103";

        private static readonly DiagnosticDescriptor RuleMismatch = new DiagnosticDescriptor(
            DiagnosticIdMismatch, 
            "NetworkId Mismatch", 
            "NetworkId '{0}' is mapped to '{1}' in NetworkIds.json, but used by '{2}' here. Did you rename the class? Please update NetworkIds.json or revert the ID.", 
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
                var networkIdsFile = additionalFiles.FirstOrDefault(file => file.Path.EndsWith("NetworkIds.json", StringComparison.OrdinalIgnoreCase));

                var idToNameMap = new Dictionary<int, string>();

                if (networkIdsFile != null)
                {
                    var sourceText = networkIdsFile.GetText(compilationContext.CancellationToken);
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
                    var networkIdAttr = attributes.FirstOrDefault(attr => 
                        attr.AttributeClass != null && 
                        (attr.AttributeClass.Name == "NetworkIdAttribute" || attr.AttributeClass.Name == "NetworkId"));

                    if (networkIdAttr == null || networkIdAttr.ConstructorArguments.Length == 0)
                        return;

                    if (networkIdAttr.ConstructorArguments[0].Value is int id)
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
