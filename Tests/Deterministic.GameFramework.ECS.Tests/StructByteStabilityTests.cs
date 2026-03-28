using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

/// <summary>
/// Tests that prove struct padding causes byte-level instability when copying raw memory,
/// and that [StructLayout(Sequential, Pack=1)] fixes it.
/// </summary>
public class StructByteStabilityTests
{
    /// <summary>
    /// Demonstrates the padding problem: a struct with bool followed by a larger-aligned field
    /// has padding bytes that retain whatever garbage was in memory before field assignment.
    ///
    /// Without Pack=1, setting fields individually does NOT touch padding bytes,
    /// so two structs with identical field values can have different raw bytes.
    /// </summary>
    [Fact]
    public unsafe void ComponentStructs_WithPadding_ProduceDifferentBytes_WhenFieldsSetIndividually()
    {
        // Scan all IComponent types in loaded assemblies
        var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName != null && a.FullName.Contains("Deterministic.GameFramework"))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t.IsValueType && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
            .ToList();

        Assert.True(componentTypes.Count > 0, "No IComponent types found in loaded assemblies");

        var unstableTypes = new System.Collections.Generic.List<(Type type, int offset)>();

        foreach (var type in componentTypes)
        {
            int size = Marshal.SizeOf(type);
            if (size == 0) continue;

            // Skip structs with no instance fields (empty structs have 1-byte size but no fields to write)
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fields.Length == 0) continue;

            // Allocate two memory regions with different fill patterns
            byte* ptr1 = (byte*)NativeMemory.Alloc((nuint)size);
            byte* ptr2 = (byte*)NativeMemory.Alloc((nuint)size);

            try
            {
                Unsafe.InitBlock(ptr1, 0xAA, (uint)size);
                Unsafe.InitBlock(ptr2, 0x55, (uint)size);

                // Create default instances and copy them into the pre-filled regions
                // This simulates what happens when a system writes component fields:
                // the padding bytes are NOT overwritten by the struct copy from a fresh default.
                //
                // Actually, struct assignment copies ALL bytes (including padding from source).
                // The real issue is when fields are set individually on an existing struct.
                // But we can still detect padding by checking: does setting all fields via
                // a fresh default produce a struct where every byte is deterministic?
                //
                // Strategy: fill with pattern, then set each field individually to default value.
                // If padding exists, the pattern bytes survive in the gaps.

                SetAllFieldsToDefault(type, ptr1);
                SetAllFieldsToDefault(type, ptr2);

                var span1 = new Span<byte>(ptr1, size);
                var span2 = new Span<byte>(ptr2, size);

                if (!span1.SequenceEqual(span2))
                {
                    // Find first differing offset
                    int offset = -1;
                    for (int i = 0; i < size; i++)
                    {
                        if (span1[i] != span2[i]) { offset = i; break; }
                    }
                    unstableTypes.Add((type, offset));
                }
            }
            finally
            {
                NativeMemory.Free(ptr1);
                NativeMemory.Free(ptr2);
            }
        }

        Assert.True(unstableTypes.Count == 0,
            "The following IComponent structs have unstable byte representations due to padding:\n" +
            string.Join("\n", unstableTypes.Select(t =>
                $"  - {t.type.Name}: first diff at byte offset {t.offset} (struct size: {Marshal.SizeOf(t.type)})")));
    }

    /// <summary>
    /// Enforces that ALL IComponent structs have [StructLayout(LayoutKind.Sequential, Pack = 1)]
    /// to prevent padding bytes from causing non-deterministic raw memory comparisons.
    /// </summary>
    [Fact]
    public void AllComponentStructs_MustHave_SequentialPackOne()
    {
        var componentTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName != null && a.FullName.Contains("Deterministic.GameFramework"))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t.IsValueType && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
            // Exclude empty structs (no fields = no padding)
            .Where(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length > 0)
            .ToList();

        Assert.True(componentTypes.Count > 0, "No IComponent types found in loaded assemblies");

        var violations = new System.Collections.Generic.List<string>();

        foreach (var type in componentTypes)
        {
            var attr = type.StructLayoutAttribute;
            if (attr == null || attr.Value != LayoutKind.Sequential || attr.Pack != 1)
            {
                var current = attr == null ? "no attribute" : $"Pack={attr.Pack}, Layout={attr.Value}";
                violations.Add($"  - {type.FullName} ({current})");
            }
        }

        Assert.True(violations.Count == 0,
            "The following IComponent structs are missing [StructLayout(LayoutKind.Sequential, Pack = 1)]:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// Enforces that all IParam structs (types used as component fields that get serialized as raw bytes)
    /// also have Pack=1 to prevent padding in nested field types.
    /// </summary>
    [Fact]
    public void AllParamStructs_MustHave_SequentialPackOne()
    {
        var paramInterface = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName != null && a.FullName.Contains("Deterministic.GameFramework"))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .FirstOrDefault(t => t.Name == "IParam" && t.IsInterface);

        if (paramInterface == null)
        {
            // IParam not found in loaded assemblies, skip
            return;
        }

        var paramTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName != null && a.FullName.Contains("Deterministic.GameFramework"))
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return Array.Empty<Type>(); }
            })
            .Where(t => t.IsValueType && !t.IsAbstract && paramInterface.IsAssignableFrom(t))
            .Where(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Length > 0)
            .ToList();

        var violations = new System.Collections.Generic.List<string>();

        foreach (var type in paramTypes)
        {
            var attr = type.StructLayoutAttribute;
            if (attr == null || attr.Value != LayoutKind.Sequential || attr.Pack != 1)
            {
                var current = attr == null ? "no attribute" : $"Pack={attr.Pack}, Layout={attr.Value}";
                violations.Add($"  - {type.FullName} ({current})");
            }
        }

        Assert.True(violations.Count == 0,
            "The following IParam structs are missing [StructLayout(LayoutKind.Sequential, Pack = 1)]:\n" +
            string.Join("\n", violations));
    }

    /// <summary>
    /// Sets every instance field of a struct to its default value, one field at a time.
    /// This simulates how game systems write to component fields individually,
    /// which does NOT touch padding bytes between fields.
    /// </summary>
    private static unsafe void SetAllFieldsToDefault(Type structType, byte* target)
    {
        var fields = structType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (var field in fields)
        {
            var fieldOffset = Marshal.OffsetOf(structType, field.Name);
            var fieldSize = Marshal.SizeOf(field.FieldType);

            // Create a zero-filled default value for this field type
            var defaultValue = new byte[fieldSize];
            // defaultValue is already all zeros (which is the default for all our types)

            fixed (byte* srcPtr = defaultValue)
            {
                Buffer.MemoryCopy(srcPtr, target + (int)fieldOffset, fieldSize, fieldSize);
            }
        }
    }
}
