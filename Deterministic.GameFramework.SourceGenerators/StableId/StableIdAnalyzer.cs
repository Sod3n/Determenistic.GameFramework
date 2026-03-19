using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StableIdAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF100";
        private const string Title = "Missing StableId attribute";
        private const string MessageFormat = "Type '{0}' inherits from a networked base class but lacks a [StableId] attribute";
        private const string Description = "All networked action and reaction types must be decorated with [StableId(int)] to track versioning.";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            if (namedTypeSymbol.TypeKind != TypeKind.Class && namedTypeSymbol.TypeKind != TypeKind.Struct)
                return;

            if (namedTypeSymbol.IsAbstract)
                return;

            var baseType = namedTypeSymbol.BaseType;
            bool requiresStableId = false;

            // Check base classes
            while (baseType != null)
            {
                var baseName = baseType.Name;
                if (baseName.Contains("ActionService") || baseName.Contains("ReactionService"))
                {
                    return;
                }
                baseType = baseType.BaseType;
            }

            // Check interfaces
            if (!requiresStableId)
            {
                foreach (var iface in namedTypeSymbol.AllInterfaces)
                {
                    if (iface.Name == "IComponent")
                    {
                        requiresStableId = true;
                        break;
                    }
                }
            }

            if (!requiresStableId)
                return;

            var attributes = namedTypeSymbol.GetAttributes();
            bool hasStableId = attributes.Any(attr => 
                attr.AttributeClass != null && 
                (attr.AttributeClass.Name == "StableIdAttribute" || attr.AttributeClass.Name == "StableId"));

            if (!hasStableId)
            {
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
