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

        sb.AppendLine();
        sb.AppendLine("--- Raw Memory Hex Dump ---");
        try
        {
            byte[] rawData = StateSerializer.Serialize(state);
            sb.AppendLine($"Total Bytes: {rawData.Length}");
            sb.AppendLine(ToHexString(rawData));
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Error generating raw dump: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string DumpComponent(object component)
    {
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

    private static string ToHexString(byte[] bytes)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < bytes.Length; i += 16)
        {
            // 1. Output Offset
            sb.Append(i.ToString("X8"));
            sb.Append("  ");

            // 2. Output Hex
            for (int j = 0; j < 16; j++)
            {
                if (i + j < bytes.Length)
                    sb.Append(bytes[i + j].ToString("X2") + " ");
                else
                    sb.Append("   "); // Padding for partial lines

                if (j == 7) sb.Append(" "); // Extra space for visual grouping
            }

            sb.Append(" |");

            // 3. Output ASCII
            for (int j = 0; j < 16; j++)
            {
                if (i + j < bytes.Length)
                {
                    char c = (char)bytes[i + j];
                    // Only print printable chars, otherwise dot
                    sb.Append((c >= 32 && c <= 126) ? c : '.');
                }
            }
            sb.Append("|");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
