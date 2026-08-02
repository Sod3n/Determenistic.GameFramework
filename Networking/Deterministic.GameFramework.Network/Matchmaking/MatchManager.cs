using System;
using System.Collections.Concurrent;
using System.IO;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Debugging;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Network.Server;

public interface IMatchFactory
{
    Match CreateMatch(System.Guid matchId, byte[]? initialState = null);
}

public class MatchManager
{
    private readonly ConcurrentDictionary<System.Guid, Match> _matches = new();
    private readonly ConcurrentDictionary<System.Guid, SyncMode> _matchModes = new();
    private readonly IMatchFactory _factory;
    private readonly INetworkServer _networkServer;

    public event Action<Match>? OnMatchCreated;
    public event Action<Match>? OnMatchRemoved;

    public MatchManager(IMatchFactory factory, INetworkServer networkServer)
    {
        _factory = factory;
        _networkServer = networkServer;
    }

    public SyncMode GetMatchMode(System.Guid matchId)
        => _matchModes.TryGetValue(matchId, out var mode) ? mode : SyncMode.GGPO;

    public Match CreateMatch(System.Guid matchId, byte[]? initialState = null, SyncMode mode = SyncMode.GGPO)
    {
        if (_matches.ContainsKey(matchId))
        {
            throw new InvalidOperationException($"Match {matchId} already exists.");
        }

        var match = _factory.CreateMatch(matchId, initialState);

        var plugin = SyncModePluginRegistry.Get(mode);
        if (plugin != null)
            plugin.OnMatchCreated(match, match.Loop.Simulation, _networkServer);

        if (Environment.GetEnvironmentVariable("DESYNC_RECORDING") == "true")
        {
            var logDir = Environment.GetEnvironmentVariable("DESYNC_LOG_DIR") ?? "desync-logs";
            var recorder = new DesyncRecorder(match.Loop.Simulation);
            recorder.Start(
                Path.Combine(logDir, $"server_{matchId}.jsonl"),
                "server",
                matchId.ToString(),
                match.Loop.TickRate);
            match.Loop.Simulation.OnRecordTick = recorder.RecordTick;
            match.AddService(recorder);
        }

        _matchModes[matchId] = mode;

        if (!_matches.TryAdd(matchId, match))
        {
             match.Dispose();
             _matchModes.TryRemove(matchId, out _);
             throw new InvalidOperationException($"Match {matchId} was created concurrently.");
        }

        _ = match.Loop.Start($"GameLoop-{match.Id:N}".Substring(0, 24));

        OnMatchCreated?.Invoke(match);

        return match;
    }

    public Match? GetMatch(System.Guid matchId)
    {
        _matches.TryGetValue(matchId, out var match);
        return match;
    }

    public bool RemoveMatch(System.Guid matchId)
    {
        if (_matches.TryRemove(matchId, out var match))
        {
            _matchModes.TryRemove(matchId, out _);
            OnMatchRemoved?.Invoke(match);
            match.Dispose();
            return true;
        }
        return false;
    }
}
