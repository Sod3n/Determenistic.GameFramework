using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NetworkIdAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF100";
        private const string Title = "Missing NetworkId attribute";
        private const string MessageFormat = "Type '{0}' inherits from a networked base class but lacks a [NetworkId] attribute";
        private const string Description = "All networked action and reaction types must be decorated with [NetworkId(int)] to track versioning.";
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
            bool requiresNetworkId = false;

            // Check base classes
            while (baseType != null)
            {
                var baseName = baseType.Name;
                if (baseName == "ActionService" || baseName == "ReactionService")
                {
                    requiresNetworkId = true;
                    break;
                }
                baseType = baseType.BaseType;
            }

            // Check interfaces
            if (!requiresNetworkId)
            {
                foreach (var iface in namedTypeSymbol.AllInterfaces)
                {
                    if (iface.Name == "IComponent")
                    {
                        requiresNetworkId = true;
                        break;
                    }
                }
            }

            if (!requiresNetworkId)
                return;

            var attributes = namedTypeSymbol.GetAttributes();
            bool hasNetworkId = attributes.Any(attr => 
                attr.AttributeClass != null && 
                (attr.AttributeClass.Name == "NetworkIdAttribute" || attr.AttributeClass.Name == "NetworkId"));

            if (!hasNetworkId)
            {
                var diagnostic = Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
