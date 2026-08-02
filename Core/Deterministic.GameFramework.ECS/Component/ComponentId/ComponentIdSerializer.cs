using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.ECS;

public static class ComponentIdSerializer
{
    private static List<KeyValuePair<StableComponentId, DenseComponentId>>? _cachedSnapshot;
    private static int _cachedCount = -1;

    /// <summary>
    /// Returns a sorted snapshot of the current StableId → DenseId mappings. Cached across
    /// calls and only rebuilt when the registry's size changes — mappings are append-only
    /// after startup in normal operation, so after the first call this becomes an O(1) field
    /// read. Previously rebuilt + sorted a full list per call, which showed up as ~5 k VPS
    /// trace-units in per-broadcast paths.
    /// </summary>
    public static List<KeyValuePair<StableComponentId, DenseComponentId>> GetMappingsSnapshot()
    {
        int currentCount = ComponentId.StableToDense.Count;
        var cached = _cachedSnapshot;
        if (cached != null && _cachedCount == currentCount)
            return cached;

        var fresh = new List<KeyValuePair<StableComponentId, DenseComponentId>>(ComponentId.StableToDense);
        fresh.Sort((a, b) => a.Key.Value.CompareTo(b.Key.Value));
        _cachedSnapshot = fresh;
        _cachedCount = currentCount;
        return fresh;
    }

    /// <summary>Invalidate the snapshot cache. Call after <c>ComponentId.ClearMappings</c> in tests.</summary>
    public static void InvalidateCache()
    {
        _cachedSnapshot = null;
        _cachedCount = -1;
    }
}
