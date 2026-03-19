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
            
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var allowedTypes = GetAllowedTypes(compilationContext.Options.AdditionalFiles);
                compilationContext.RegisterSymbolAction(ctx => AnalyzeSymbol(ctx, allowedTypes), SymbolKind.NamedType);
            });
        }

        private static ImmutableHashSet<string> GetAllowedTypes(ImmutableArray<AdditionalText> additionalFiles)
        {
            var builder = ImmutableHashSet.CreateBuilder<string>();

            // Internal default whitelist
            // Note: primitives and IParam types are handled dynamically.
            // These are for types that might NOT implement IParam but are considered safe
            // or specific overrides.
            builder.Add("Guid");
            builder.Add("System.Guid"); 
            builder.Add("Vector2");
            builder.Add("Vector3");
            builder.Add("Entity");
            builder.Add("Ref");
            builder.Add("FixedString32");
            builder.Add("BitMask128");

            foreach (var file in additionalFiles)
            {
                if (file.Path.EndsWith(".Deterministic.Allowed.txt", StringComparison.OrdinalIgnoreCase) || 
                    file.Path.EndsWith("DeterministicSafety.Allowed.txt", StringComparison.OrdinalIgnoreCase))
                {
                    var text = file.GetText();
                    if (text != null)
                    {
                        foreach (var line in text.Lines)
                        {
                            var typeName = line.ToString().Trim();
                            if (!string.IsNullOrEmpty(typeName) && !typeName.StartsWith("#"))
                            {
                                builder.Add(typeName);
                            }
                        }
                    }
                }
            }
            
            return builder.ToImmutable();
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ImmutableHashSet<string> whitelist)
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

                if (!IsDeterministic(field.Type, whitelist))
                {
                    var diagnostic = Diagnostic.Create(Rule, field.Locations[0], field.Name, namedTypeSymbol.Name, field.Type.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool IsDeterministic(ITypeSymbol type, ImmutableHashSet<string> whitelist)
        {
            // 1. Primitives
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
                
                // Float/Double/String/Object are explicitly NOT deterministic
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                case SpecialType.System_Object:
                    return false;
            }

            // 2. Enums are safe
            if (type.TypeKind == TypeKind.Enum)
                return true;

            // 3. Arrays are unsafe
            if (type.TypeKind == TypeKind.Array)
                return false;

            string name = type.Name;
            string fullName = type.ToDisplayString();

            // 4. Check Whitelist
            if (whitelist.Contains(name) || whitelist.Contains(fullName))
                return true;

            // 5. Special Generics (List8<T>)
            if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                if (name == "List8")
                {
                    // Check generic argument
                    var arg = namedType.TypeArguments[0];
                    return IsDeterministic(arg, whitelist);
                }
            }

            // 6. Check IParam
            if (type.AllInterfaces.Any(i => i.Name == "IParam"))
            {
                return true;
            }

            // 7. Recursive Struct Check
            if (type.TypeKind == TypeKind.Struct)
            {
                if (type is INamedTypeSymbol namedStruct)
                {
                    return CheckStructFieldsRecursively(namedStruct, whitelist);
                }
            }

            return false;
        }

        private static bool CheckStructFieldsRecursively(INamedTypeSymbol structType, ImmutableHashSet<string> whitelist)
        {
            foreach (var field in structType.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst) continue;
                
                // Recursively check each field
                if (!IsDeterministic(field.Type, whitelist))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
