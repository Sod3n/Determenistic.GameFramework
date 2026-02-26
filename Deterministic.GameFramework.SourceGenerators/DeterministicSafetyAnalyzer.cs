using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Deterministic.GameFramework.SourceGenerators
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DeterministicSafetyAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DGF200";
        private const string Title = "Non-deterministic field type";
        private const string MessageFormat = "Field '{0}' in '{1}' is of non-deterministic or unsafe type '{2}'";
        private const string Description = "Networked components and actions must only contain deterministic types (Float, Int, Vector2, Entity, etc.) and avoid float, double, or reference types.";
        private const string Category = "Determinism";

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

            // Only check structs
            if (namedTypeSymbol.TypeKind != TypeKind.Struct)
                return;

            // Check if it implements IComponent or IAction
            bool isNetworked = namedTypeSymbol.AllInterfaces.Any(i => 
                i.Name == "IComponent" || 
                i.Name == "IAction");

            if (!isNetworked)
                return;

            foreach (var field in namedTypeSymbol.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst) continue;

                if (!IsDeterministic(field.Type))
                {
                    var diagnostic = Diagnostic.Create(Rule, field.Locations[0], field.Name, namedTypeSymbol.Name, field.Type.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsDeterministic(ITypeSymbol type)
        {
            // Primitives
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Byte:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                    return true;
                
                // Float/Double are explicitly NOT deterministic
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String: // Strings are refs and allocs
                case SpecialType.System_Object:
                    return false;
            }

            // Enums are safe if their underlying type is safe (int by default)
            if (type.TypeKind == TypeKind.Enum)
                return true;

            // Arrays are reference types -> unsafe for this strict struct-only model
            if (type.TypeKind == TypeKind.Array)
                return false;

            // Check specific allowed types by name
            string name = type.Name;
            if (name == "Float" || 
                name == "Int" || 
                name == "Vector2" || 
                name == "Vector3" || 
                name == "Entity" || 
                name == "Ref" || 
                name == "FixedString32" ||
                name == "BitMask128")
            {
                return true;
            }

            // Generic types like List8<T>
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                if (name == "List8")
                {
                    // Check generic argument
                    var arg = namedType.TypeArguments[0];
                    return IsDeterministic(arg);
                }
            }

            // For other structs, recursively check fields.
            // Structs form a DAG (no cycles), so recursion is safe.
            if (type.TypeKind == TypeKind.Struct)
            {
                if (type is INamedTypeSymbol namedStruct)
                {
                    return CheckStructFieldsRecursively(namedStruct);
                }
            }

            return false;
        }

        private static bool CheckStructFieldsRecursively(INamedTypeSymbol structType)
        {
            foreach (var field in structType.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst) continue;
                
                // Recursively check each field
                if (!IsDeterministic(field.Type))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
