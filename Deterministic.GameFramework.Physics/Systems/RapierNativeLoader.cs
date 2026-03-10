using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Physics.Systems;

public static class RapierNativeLoader
{
    public static void Initialize()
    {
#if NETCOREAPP3_0_OR_GREATER
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(RapierPhysicsSystem).Assembly, DllImportResolver);
        }
        catch (InvalidOperationException)
        {
            // Resolver already set, which is fine.
        }
#endif
    }

#if NETCOREAPP3_0_OR_GREATER
    private static IntPtr DllImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName == "rapier_uniffi")
        {
            string root;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                root = "librapier_uniffi.dylib";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                root = "librapier_uniffi.so";
            }
            else
            {
                root = "rapier_uniffi.dll";
            }

            // 1. Try loading with the platform specific name (Standard search path)
            if (NativeLibrary.TryLoad(root, assembly, searchPath, out var handle))
            {
                return handle;
            }

            // 2. Try looking in the same directory as the assembly (Godot / Local build often needs this)
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                string assemblyDir = Path.GetDirectoryName(assembly.Location)!;
                string localPath = Path.Combine(assemblyDir, root);
                if (File.Exists(localPath) && NativeLibrary.TryLoad(localPath, out handle))
                {
                    return handle;
                }
            }

            // 3. Try AppContext.BaseDirectory
            string baseDir = AppContext.BaseDirectory;
            string basePath = Path.Combine(baseDir, root);
            if (File.Exists(basePath) && NativeLibrary.TryLoad(basePath, out handle))
            {
                return handle;
            }

            // 4. Fallback: Try looking in runtimes folder if not found in root
            string runtimePath;
             if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                runtimePath = Path.Combine("runtimes", "osx", "native", root);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                 runtimePath = Path.Combine("runtimes", "linux", "native", root);
            }
            else 
            {
                 runtimePath = Path.Combine("runtimes", "win", "native", root);
            }
            
            // Try relative to assembly location
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                string assemblyDir = Path.GetDirectoryName(assembly.Location)!;
                string localRuntimePath = Path.Combine(assemblyDir, runtimePath);
                 if (File.Exists(localRuntimePath) && NativeLibrary.TryLoad(localRuntimePath, out handle))
                {
                    return handle;
                }
            }

            // Try relative to BaseDirectory
            string baseRuntimePath = Path.Combine(baseDir, runtimePath);
            if (File.Exists(baseRuntimePath) && NativeLibrary.TryLoad(baseRuntimePath, out handle))
            {
                return handle;
            }
        }

        return IntPtr.Zero;
    }
#endif
}
