using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StableIdConflictAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF101";
        private const string Title = "Duplicate StableId";
        private const string MessageFormat = "StableId '{0}' is already used by another type in this assembly";
        private const string Description = "StableIds must be unique across the entire assembly.";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var idMap = new ConcurrentDictionary<int, string>();
                
                compilationContext.RegisterSymbolAction(symbolContext => 
                {
                    var namedTypeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                    var attributes = namedTypeSymbol.GetAttributes();
                    var StableIdAttr = attributes.FirstOrDefault(attr => 
                        attr.AttributeClass != null && 
                        (attr.AttributeClass.Name == "StableIdAttribute" || attr.AttributeClass.Name == "StableId"));

                    if (StableIdAttr == null || StableIdAttr.ConstructorArguments.Length == 0)
                        return;

                    if (StableIdAttr.ConstructorArguments[0].Value is int id)
                    {
                        var fullName = namedTypeSymbol.ToDisplayString();
                        
                        // Try to add. If it exists and the full name is different, we have a conflict.
                        if (!idMap.TryAdd(id, fullName))
                        {
                            if (idMap[id] != fullName)
                            {
                                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], id);
                                symbolContext.ReportDiagnostic(diagnostic);
                            }
                        }
                    }
                }, SymbolKind.NamedType);
            });
        }
    }
}
