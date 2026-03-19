using System;
using System.Text;

namespace Deterministic.GameFramework.ECS;

public static class StateDumper
{

    public static string Dump(EntityWorld state)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- State Dump ---");
        
        // Dump Entities and Components
        for (int i = 0; i < state.EntityMasks.Length; i++)
        {
            if (state.EntityMasks[i].IsEmpty) continue;
            
            sb.AppendLine($"Entity {i}:");
            
            // We need to iterate all possible component types.
            // Since we don't have a direct list of components per entity without reflection or iteration
            // We can iterate the component arrays.
            
            for (int typeId = 0; typeId < state._componentArrays.Length; typeId++)
            {
                if (state.EntityMasks[i].IsSet(typeId) && state._componentArrays[typeId] is { } array)
                {
                    var component = array.GetValue(i);
                    if (component != null)
                    {
                        sb.AppendLine($"  {component.GetType().Name}: {DumpComponent(component)}");
                    }
                }
            }
        }
        
        return sb.ToString();
    }

    private static string DumpComponent(object component)
    {
        // Use reflection to dump public fields for other components
        var type = component.GetType();
        if (type.IsValueType)
        {
            var sb = new StringBuilder();
            sb.Append("{ ");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                sb.Append($"{field.Name}: {field.GetValue(component)}, ");
            }
            if (sb.Length > 2) sb.Length -= 2; // Remove trailing comma
            sb.Append(" }");
            return sb.ToString();
        }

        return component.ToString() ?? "null";
    }
}
