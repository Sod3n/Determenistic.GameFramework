using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.NetworkV2.Server;

public interface IMatchFactory
{
    Match CreateMatch(System.Guid matchId);
}

public class MatchManager
{
    private readonly ConcurrentDictionary<System.Guid, Match> _matches = new();
    private readonly IMatchFactory _factory;

    public event Action<Match>? OnMatchCreated;
    public event Action<Match>? OnMatchRemoved;

    public MatchManager(IMatchFactory factory)
    {
        _factory = factory;
    }

    public Match CreateMatch(System.Guid matchId)
    {
        if (_matches.ContainsKey(matchId))
        {
            throw new InvalidOperationException($"Match {matchId} already exists.");
        }

        var match = _factory.CreateMatch(matchId);
        if (!_matches.TryAdd(matchId, match))
        {
             // Should not happen due to check above, but for thread safety
             match.Dispose();
             throw new InvalidOperationException($"Match {matchId} was created concurrently.");
        }

        // Start the game loop
        _ = match.Loop.Start();
        
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
            OnMatchRemoved?.Invoke(match);
            match.Dispose();
            return true;
        }
        return false;
    }
}
