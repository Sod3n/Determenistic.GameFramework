using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class StructLayoutAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF300";
        private const string Title = "Missing StructLayout(Sequential, Pack = 1)";
        private const string MessageFormat = "Struct '{0}' implements '{1}' but lacks [StructLayout(LayoutKind.Sequential, Pack = 1)] — raw byte copying may produce non-deterministic results due to padding";
        private const string Description = "All IComponent and IParam structs must have [StructLayout(LayoutKind.Sequential, Pack = 1)] to eliminate padding bytes that cause non-deterministic raw memory comparisons.";
        private const string Category = "Determinism";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        private static readonly string[] RequiredInterfaces = { "IComponent", "IParam" };

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var namedType = (INamedTypeSymbol)context.Symbol;

            if (namedType.TypeKind != TypeKind.Struct)
                return;

            if (namedType.IsAbstract)
                return;

            // Check if it has any instance fields (empty structs don't need Pack=1)
            var hasInstanceFields = namedType.GetMembers()
                .OfType<IFieldSymbol>()
                .Any(f => !f.IsStatic && !f.IsConst);

            if (!hasInstanceFields)
                return;

            // Find which required interface this struct implements
            string matchedInterface = null;
            foreach (var iface in namedType.AllInterfaces)
            {
                foreach (var required in RequiredInterfaces)
                {
                    if (iface.Name == required)
                    {
                        matchedInterface = required;
                        break;
                    }
                }
                if (matchedInterface != null) break;
            }

            if (matchedInterface == null)
                return;

            // Check if it already has StructLayout with Pack=1
            if (HasCorrectStructLayout(namedType))
                return;

            var diagnostic = Diagnostic.Create(Rule, namedType.Locations[0], namedType.Name, matchedInterface);
            context.ReportDiagnostic(diagnostic);
        }

        internal static bool HasCorrectStructLayout(INamedTypeSymbol namedType)
        {
            foreach (var attr in namedType.GetAttributes())
            {
                if (attr.AttributeClass == null)
                    continue;

                var attrName = attr.AttributeClass.Name;
                if (attrName != "StructLayoutAttribute" && attrName != "StructLayout")
                    continue;

                // Check constructor arg: LayoutKind.Sequential (value = 0)
                if (attr.ConstructorArguments.Length < 1)
                    continue;

                var layoutKindArg = attr.ConstructorArguments[0];
                if (layoutKindArg.Value == null)
                    continue;

                int layoutKindValue = (int)layoutKindArg.Value;
                // LayoutKind.Sequential = 0
                if (layoutKindValue != 0)
                    continue;

                // Check named arg: Pack = 1
                foreach (var namedArg in attr.NamedArguments)
                {
                    if (namedArg.Key == "Pack" && namedArg.Value.Value is int pack && pack == 1)
                        return true;
                }
            }

            return false;
        }
    }
}
