using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        var sourceComponents = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is StructDeclarationSyntax,
                transform: (ctx, _) => GetComponentInfo(ctx))
            .Where(m => m != null);

        // Find all types with EntityDefinitionAttribute
        var entityResults = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => node is TypeDeclarationSyntax t && t.AttributeLists.Count > 0,
                transform: (ctx, _) => GetEntityAndComponentInfo(ctx))
            .Where(m => m != null);

        var entities = entityResults.Select((x, _) => x!.Entity);
        var referencedComponents = entityResults.Select((x, _) => x!.Components);

        // Combine all components (source + referenced)
        var allComponents = sourceComponents.Collect()
            .Combine(referencedComponents.Collect())
            .Select((pair, _) => 
            {
                var list = new List<ComponentInfo>(pair.Left!);
                foreach (var sublist in pair.Right)
                {
                    if (sublist != null)
                    {
                        list.AddRange(sublist);
                    }
                }
                
                // Deduplicate by StructName (and Namespace ideally, but mostly StructName matters for filename)
                var distinct = new Dictionary<string, ComponentInfo>();
                foreach (var comp in list)
                {
                    var key = $"{comp.Namespace}.{comp.StructName}";
                    if (!distinct.ContainsKey(key))
                    {
                        distinct[key] = comp;
                    }
                }
                return distinct.Values.ToList();
            });

        // Generate the models only if ReactiveSystem exists
        var componentsWithCompilation = allComponents.Combine(context.CompilationProvider);
        var generateInput = componentsWithCompilation.Combine(reactiveSystemRef).Combine(isGodot);

        context.RegisterSourceOutput(generateInput, (spc, source) => 
        {
            var (components, compilation) = source.Left.Left;
            var hasReactive = source.Left.Right;
            var godotMode = source.Right;
            
            if (hasReactive)
            {
                foreach (var comp in components)
                {
                    // Check if the model already exists in the compilation (referenced assemblies)
                    var modelFullName = $"Deterministic.GameFramework.Reactive.{comp.StructName}Model";
                    if (compilation.GetTypeByMetadataName(modelFullName) != null)
                    {
                        continue;
                    }

                    Execute(spc, comp, godotMode);
                }
            }
        });

        // Generate the shared ComponentExtensions
        context.RegisterSourceOutput(generateInput, (spc, source) => 
        {
            var (components, compilation) = source.Left.Left;
            var hasReactive = source.Left.Right;

            if (hasReactive)
            {
                var uniqueComponents = new List<ComponentInfo?>();
                foreach (var comp in components)
                {
                     var modelFullName = $"Deterministic.GameFramework.Reactive.{comp.StructName}Model";
                     if (compilation.GetTypeByMetadataName(modelFullName) != null)
                     {
                         continue;
                     }
                     uniqueComponents.Add(comp);
                }
                
                if (uniqueComponents.Count > 0)
                {
                    ExecuteExtensions(spc, uniqueComponents.ToImmutableArray());
                }
            }
        });

        // Generate Entity Models
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

        return ExtractComponentInfoFromSymbol(symbol);
    }

    private static ComponentInfo? ExtractComponentInfoFromSymbol(INamedTypeSymbol symbol)
    {
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

        var properties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && p.GetMethod != null && p.SetMethod != null)
            .Select(p => new FieldInfo 
            { 
                Name = p.Name, 
                Type = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) 
            });
            
        fields.AddRange(properties);

        return new ComponentInfo
        {
            Namespace = namespaceName,
            StructName = structName,
            Fields = fields
        };
    }

    private class EntityGenResult
    {
        public EntityInfo? Entity { get; set; }
        public List<ComponentInfo> Components { get; set; } = new();
    }

    private static EntityGenResult? GetEntityAndComponentInfo(GeneratorSyntaxContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        var attr = symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == "EntityDefinitionAttribute" || a.AttributeClass?.Name == "EntityDefinition");
        if (attr == null) return null;

        var components = new List<string>();
        var componentInfos = new List<ComponentInfo>();

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
                        
                        var compInfo = ExtractComponentInfoFromSymbol(typeSymbol);
                        if (compInfo != null)
                        {
                            componentInfos.Add(compInfo);
                        }
                    }
                }
            }
        }

        var entityInfo = new EntityInfo
        {
            Namespace = symbol.ContainingNamespace.ToDisplayString(),
            Name = symbol.Name,
            ComponentNames = components
        };

        return new EntityGenResult
        {
            Entity = entityInfo,
            Components = componentInfos
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
        // Normalize type name to avoid issues with global:: prefix
        var normalized = type.StartsWith("global::") ? type.Substring(8) : type;
        
        // Always map these types to standard C# primitives/types
        if (normalized == "Deterministic.GameFramework.CoreV2.Float") return "float";
        if (normalized == "Deterministic.GameFramework.CoreV2.Int") return "int";
        if (normalized == "Deterministic.GameFramework.CoreV2.FixedString32") return "string";
        if (normalized == "Deterministic.GameFramework.CoreV2.Ref") return "int";
        if (normalized == "Deterministic.GameFramework.CoreV2.Entity") return "int";
        if (normalized == "Deterministic.GameFramework.CoreV2.Guid") return "global::System.Guid";

        if (!isGodot) return type;
        
        if (normalized == "Deterministic.GameFramework.CoreV2.Vector2") return "global::Godot.Vector2";
        if (normalized == "Deterministic.GameFramework.CoreV2.Vector3") return "global::Godot.Vector3";
        
        return type;
    }

    private string GetConversion(string varName, string type, bool isGodot)
    {
        var normalized = type.StartsWith("global::") ? type.Substring(8) : type;

        // Always convert these types
        if (normalized == "Deterministic.GameFramework.CoreV2.Float")
        {
            return $"(float){varName}";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.Int")
        {
            return $"(int){varName}";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.FixedString32")
        {
            return $"{varName}.ToString()";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.Ref")
        {
            return $"(int){varName}";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.Entity")
        {
            return $"(int){varName}";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.Guid")
        {
            return $"(global::System.Guid){varName}";
        }

        if (!isGodot) return varName;

        if (normalized == "Deterministic.GameFramework.CoreV2.Vector2") 
        {
            return $"new global::Godot.Vector2((float){varName}.X, (float){varName}.Y)";
        }
        if (normalized == "Deterministic.GameFramework.CoreV2.Vector3") 
        {
            return $"new global::Godot.Vector3((float){varName}.X, (float){varName}.Y, (float){varName}.Z)";
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
