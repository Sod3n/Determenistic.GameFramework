using System.Collections.Generic;
using Deterministic.GameFramework.Common;

namespace Deterministic.GameFramework.Network.Server;

public static class SyncModePluginRegistry
{
    private static readonly Dictionary<SyncMode, ISyncModePlugin> _plugins = new();

    public static void Register(SyncMode mode, ISyncModePlugin plugin)
    {
        _plugins[mode] = plugin;
    }

    public static ISyncModePlugin? Get(SyncMode mode)
    {
        _plugins.TryGetValue(mode, out var plugin);
        return plugin;
    }
}
