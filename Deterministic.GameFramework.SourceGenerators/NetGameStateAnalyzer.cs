using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NetGameStateAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF001";
        private const string Title = "Invalid field type in INetGameState";
        private const string MessageFormat = "Field '{0}' of type '{1}' does not implement INetState";
        private const string Description = "All fields in a type implementing INetGameState must implement INetState.";
        private const string Category = "Design";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public const string StructDiagnosticId = "DGF002";
        private const string StructTitle = "INetGameState must be a struct";
        private const string StructMessageFormat = "Type '{0}' implements INetGameState but is not a struct";
        private const string StructDescription = "All types implementing INetGameState must be structs.";

        private static readonly DiagnosticDescriptor StructRule = new DiagnosticDescriptor(
            StructDiagnosticId, StructTitle, StructMessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: StructDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, StructRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find INetGameState and INetState
            var inetGameStateSymbol = context.Compilation.GetTypeByMetadataName("Deterministic.GameFramework.Network.NetworkState.INetGameState");
            var inetStateSymbol = context.Compilation.GetTypeByMetadataName("Deterministic.GameFramework.Network.NetworkState.INetState");

            if (inetGameStateSymbol == null || inetStateSymbol == null)
            {
                return;
            }

            // Check if the current type implements INetGameState
            if (!namedTypeSymbol.AllInterfaces.Contains(inetGameStateSymbol, SymbolEqualityComparer.Default))
            {
                return;
            }

            // Enforce that the type is a struct (ValueType)
            if (namedTypeSymbol.TypeKind != TypeKind.Struct)
            {
                var diagnostic = Diagnostic.Create(StructRule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Iterate over fields
            var fields = namedTypeSymbol.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic && !f.IsImplicitlyDeclared);
            foreach (var field in fields)
            {
                var fieldType = field.Type;

                // Check if the field type implements INetState
                bool implementsINetState = fieldType.AllInterfaces.Contains(inetStateSymbol, SymbolEqualityComparer.Default) || 
                                           SymbolEqualityComparer.Default.Equals(fieldType, inetStateSymbol);

                if (!implementsINetState)
                {
                    var diagnostic = Diagnostic.Create(Rule, field.Locations[0], field.Name, fieldType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Also check properties just in case
            var properties = namedTypeSymbol.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic && !p.IsImplicitlyDeclared);
            foreach (var property in properties)
            {
                var propertyType = property.Type;

                bool implementsINetState = propertyType.AllInterfaces.Contains(inetStateSymbol, SymbolEqualityComparer.Default) || 
                                           SymbolEqualityComparer.Default.Equals(propertyType, inetStateSymbol);

                if (!implementsINetState)
                {
                    var diagnostic = Diagnostic.Create(Rule, property.Locations[0], property.Name, propertyType.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
