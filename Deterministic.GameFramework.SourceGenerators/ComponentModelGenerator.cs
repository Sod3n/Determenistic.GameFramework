using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Deterministic.GameFramework.SourceGenerators;

[Generator]
public class ComponentModelGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Check if the ReactiveSystem type exists in the compilation
        var reactiveSystemRef = context.CompilationProvider.Select((compilation, _) => 
            compilation.GetTypeByMetadataName("Deterministic.GameFramework.Reactive.ReactiveSystem") != null);

        // Check for GODOT symbol
        var isGodot = context.ParseOptionsProvider.Select((options, _) => 
            options.PreprocessorSymbolNames.Contains("GODOT"));

        // Find all structs that implement IComponent
        var components = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is StructDeclarationSyntax,
                transform: (ctx, _) => GetComponentInfo(ctx))
            .Where(m => m != null);

        // Combine components with the check and godot flag
        var componentsWithCheck = components.Combine(reactiveSystemRef).Combine(isGodot);

        // Generate the models only if ReactiveSystem exists
        context.RegisterSourceOutput(componentsWithCheck, (spc, source) => 
        {
            var info = source.Left.Left;
            var hasReactive = source.Left.Right;
            var godotMode = source.Right;
            
            if (hasReactive)
            {
                Execute(spc, info, godotMode);
            }
        });

        // Generate the shared ComponentExtensions
        var extensions = components.Collect().Combine(reactiveSystemRef);
        context.RegisterSourceOutput(extensions, (spc, source) => 
        {
            if (source.Right)
            {
                ExecuteExtensions(spc, source.Left);
            }
        });

        // Find all types with EntityDefinitionAttribute
        var entities = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is TypeDeclarationSyntax t && t.AttributeLists.Count > 0,
                transform: (ctx, _) => GetEntityInfo(ctx))
            .Where(m => m != null);

        var entitiesWithCheck = entities.Combine(reactiveSystemRef).Combine(isGodot);
        
        context.RegisterSourceOutput(entitiesWithCheck, (spc, source) => 
        {
            var info = source.Left.Left;
            var hasReactive = source.Left.Right;
            var godotMode = source.Right;
            
            if (hasReactive)
            {
                ExecuteEntity(spc, info, godotMode);
            }
        });
    }

    private static ComponentInfo? GetComponentInfo(GeneratorSyntaxContext context)
    {
        var structDeclaration = (StructDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(structDeclaration);
        
        if (symbol == null) return null;

        // Check if implements IComponent
        if (!symbol.AllInterfaces.Any(i => i.Name == "IComponent"))
        {
            return null;
        }

        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var structName = symbol.Name;
        
        var fields = symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(f => f.DeclaredAccessibility == Accessibility.Public && !f.IsStatic)
            .Select(f => new FieldInfo 
            { 
                Name = f.Name, 
                Type = f.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) 
            })
            .ToList();

        return new ComponentInfo
        {
            Namespace = namespaceName,
            StructName = structName,
            Fields = fields
        };
    }

    private static EntityInfo? GetEntityInfo(GeneratorSyntaxContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        var attr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "EntityDefinitionAttribute" || a.AttributeClass?.Name == "EntityDefinition");
        if (attr == null) return null;

        var components = new List<string>();
        if (attr.ConstructorArguments.Length > 0)
        {
            var arg = attr.ConstructorArguments[0];
            if (!arg.IsNull && arg.Kind == TypedConstantKind.Array)
            {
                foreach (var val in arg.Values)
                {
                    if (val.Value is INamedTypeSymbol typeSymbol)
                    {
                        components.Add(typeSymbol.Name);
                    }
                }
            }
        }

        return new EntityInfo
        {
            Namespace = symbol.ContainingNamespace.ToDisplayString(),
            Name = symbol.Name,
            ComponentNames = components
        };
    }

    private void Execute(SourceProductionContext context, ComponentInfo? info, bool isGodot)
    {
        if (info == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("");
        sb.AppendLine("namespace Deterministic.GameFramework.Reactive");
        sb.AppendLine("{");
        
        var modelName = $"{info.StructName}Model";
        var viewModelName = $"{info.StructName}ViewModel";
        var structFullName = $"global::{info.Namespace}.{info.StructName}";
        
        // Generate Model
        sb.AppendLine($"    public partial class {modelName} : global::Deterministic.GameFramework.Reactive.Model");
        sb.AppendLine("    {");
        
        // Properties
        foreach (var field in info.Fields)
        {
            var fieldType = GetTargetType(field.Type, isGodot);
            var backingName = $"_{char.ToLower(field.Name[0])}{field.Name.Substring(1)}";
            
            sb.AppendLine($"        private readonly global::R3.ReactiveProperty<{fieldType}> {backingName} = new();");
            sb.AppendLine($"        public global::R3.ReadOnlyReactiveProperty<{fieldType}> {field.Name} => {backingName};");
        }
        
        sb.AppendLine("");
        
        // Constructor
        sb.AppendLine($"        public {modelName}(global::Deterministic.GameFramework.Reactive.ReactiveSystem reactive, global::Deterministic.GameFramework.CoreV2.Context context)");
        sb.AppendLine("        {");
        
        foreach (var field in info.Fields)
        {
            var backingName = $"_{char.ToLower(field.Name[0])}{field.Name.Substring(1)}";
            
            sb.AppendLine($"            reactive.Subscribe(");
            sb.AppendLine($"                context.State,");
            sb.AppendLine($"                (s) => s.GetComponent<{structFullName}>(context.Entity).{field.Name},");
            sb.AppendLine($"                (s, val) =>");
            sb.AppendLine($"                {{");
            
            var valueExpression = GetConversion("val", field.Type, isGodot);
            sb.AppendLine($"                    {backingName}.Value = {valueExpression};");
            sb.AppendLine($"                }});");
        }
        
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        context.AddSource($"{info.StructName}Model.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private void ExecuteEntity(SourceProductionContext context, EntityInfo? info, bool isGodot)
    {
        if (info == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("");
        sb.AppendLine("namespace Deterministic.GameFramework.Reactive");
        sb.AppendLine("{");
        
        var modelName = $"{info.Name}Model";
        
        // Generate Model
        sb.AppendLine($"    public partial class {modelName} : global::Deterministic.GameFramework.Reactive.Model");
        sb.AppendLine("    {");
        
        foreach (var compName in info.ComponentNames)
        {
             var compModelName = $"{compName}Model";
             var propName = compName.EndsWith("Component") ? compName.Substring(0, compName.Length - 9) : compName;
             sb.AppendLine($"        public {compModelName} {propName} {{ get; }}");
        }
        
        sb.AppendLine("");
        sb.AppendLine($"        public {modelName}(global::Deterministic.GameFramework.Reactive.ReactiveSystem reactive, global::Deterministic.GameFramework.CoreV2.Context context)");
        sb.AppendLine("        {");
        foreach (var compName in info.ComponentNames)
        {
             var compModelName = $"{compName}Model";
             var propName = compName.EndsWith("Component") ? compName.Substring(0, compName.Length - 9) : compName;
             sb.AppendLine($"            {propName} = new {compModelName}(reactive, context);");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        context.AddSource($"{info.Name}Model.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private void ExecuteExtensions(SourceProductionContext context, System.Collections.Immutable.ImmutableArray<ComponentInfo?> components)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        
        sb.AppendLine("");
        sb.AppendLine("namespace Deterministic.GameFramework.Reactive");
        sb.AppendLine("{");
        sb.AppendLine("    public static partial class ComponentExtensions");
        sb.AppendLine("    {");
        sb.AppendLine("");

        foreach (var comp in components)
        {
            if (comp == null) continue;
            
            var modelName = $"{comp.StructName}Model";
            var structFullName = $"global::{comp.Namespace}.{comp.StructName}";
            
            sb.AppendLine($"        public static {modelName} AsModel(this {structFullName} component, global::Deterministic.GameFramework.CoreV2.Context context)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {modelName}(global::Deterministic.GameFramework.Reactive.ReactiveSystem.Instance, context);");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("ComponentExtensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private string GetTargetType(string type, bool isGodot)
    {
        if (!isGodot) return type;
        
        if (type == "global::Deterministic.GameFramework.CoreV2.Vector2") return "global::Godot.Vector2";
        if (type == "global::Deterministic.GameFramework.CoreV2.Vector3") return "global::Godot.Vector3";
        if (type == "global::Deterministic.GameFramework.CoreV2.Guid") return "global::System.Guid";
        
        return type;
    }

    private string GetConversion(string varName, string type, bool isGodot)
    {
        if (!isGodot) return varName;

        if (type == "global::Deterministic.GameFramework.CoreV2.Vector2") 
        {
            return $"new global::Godot.Vector2((float){varName}.X, (float){varName}.Y)";
        }
        if (type == "global::Deterministic.GameFramework.CoreV2.Vector3") 
        {
            return $"new global::Godot.Vector3((float){varName}.X, (float){varName}.Y, (float){varName}.Z)";
        }
        if (type == "global::Deterministic.GameFramework.CoreV2.Guid")
        {
            return $"(global::System.Guid){varName}";
        }

        return varName;
    }

    private class ComponentInfo
    {
        public string Namespace { get; set; } = "";
        public string StructName { get; set; } = "";
        public List<FieldInfo> Fields { get; set; } = new();
    }

    private class EntityInfo
    {
        public string Namespace { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> ComponentNames { get; set; } = new();
    }

    private class FieldInfo
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
