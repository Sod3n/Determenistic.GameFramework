using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.CoreV2;

public static class ComponentIdSerializer
{
    public static List<KeyValuePair<StableComponentId, DenseComponentId>> GetMappingsSnapshot()
    {
        var sortedMappings = new List<KeyValuePair<StableComponentId, DenseComponentId>>(ComponentId.StableToDense);
        sortedMappings.Sort((a, b) => a.Key.Value.CompareTo(b.Key.Value));
        return sortedMappings;
    }
}
