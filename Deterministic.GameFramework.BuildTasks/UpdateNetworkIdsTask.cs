using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Deterministic.GameFramework.BuildTasks
{
    public class UpdateNetworkIdsTask : Task
    {
        [Required]
        public string TargetAssembly { get; set; } = string.Empty;

        [Required]
        public string ProjectDirectory { get; set; } = string.Empty;

        public override bool Execute()
        {
            if (!File.Exists(TargetAssembly)) 
            {
                Log.LogWarning($"[NetworkId] Assembly not found: {TargetAssembly}");
                return true;
            }

            Log.LogMessage(MessageImportance.High, $"[NetworkId] Scanning assembly: {TargetAssembly}");

            var idsFile = Path.Combine(ProjectDirectory, "NetworkIds.json");
            var currentIds = new Dictionary<string, int>();

            if (File.Exists(idsFile))
            {
                try
                {
                    var json = File.ReadAllText(idsFile);
                    currentIds = JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[NetworkId] Failed to read existing NetworkIds.json: {ex.Message}");
                }
            }

            var newIds = new Dictionary<string, int>(currentIds);
            bool hasChanges = false;

            try
            {
                using var stream = File.OpenRead(TargetAssembly);
                using var peReader = new PEReader(stream);
                var metadataReader = peReader.GetMetadataReader();

                foreach (var typeDefHandle in metadataReader.TypeDefinitions)
                {
                    var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                    int? networkId = null;

                    foreach (var attrHandle in typeDef.GetCustomAttributes())
                    {
                        var attr = metadataReader.GetCustomAttribute(attrHandle);
                        var ctorHandle = attr.Constructor;

                        string attrName = "";

                        if (ctorHandle.Kind == HandleKind.MemberReference)
                        {
                            var memberRef = metadataReader.GetMemberReference((MemberReferenceHandle)ctorHandle);
                            var parentHandle = memberRef.Parent;
                            if (parentHandle.Kind == HandleKind.TypeReference)
                            {
                                var typeRef = metadataReader.GetTypeReference((TypeReferenceHandle)parentHandle);
                                attrName = metadataReader.GetString(typeRef.Name);
                            }
                        }

                        if (attrName == "NetworkIdAttribute" || attrName == "NetworkId")
                        {
                            var value = attr.Value;
                            var bytes = metadataReader.GetBlobBytes(value);
                            
                            // Prolog is 01 00, then the int32
                            if (bytes.Length >= 6 && bytes[0] == 0x01 && bytes[1] == 0x00)
                            {
                                networkId = BitConverter.ToInt32(bytes, 2);
                            }
                        }
                    }

                    if (networkId.HasValue)
                    {
                        var ns = metadataReader.GetString(typeDef.Namespace);
                        var name = metadataReader.GetString(typeDef.Name);
                        var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                        if (!newIds.TryGetValue(fullName, out int existingId) || existingId != networkId.Value)
                        {
                            newIds[fullName] = networkId.Value;
                            hasChanges = true;
                            Log.LogMessage(MessageImportance.High, $"[NetworkId] Added/Updated: {fullName} -> {networkId.Value}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"[NetworkId] Error scanning assembly: {ex.Message}");
                return false;
            }

            if (hasChanges)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var outputJson = JsonSerializer.Serialize(newIds.OrderBy(k => k.Key).ToDictionary(k => k.Key, v => v.Value), options);
                File.WriteAllText(idsFile, outputJson);
                Log.LogMessage(MessageImportance.High, $"[NetworkId] Updated {idsFile}");
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, "[NetworkId] No changes detected.");
            }

            return true;
        }
    }
}
