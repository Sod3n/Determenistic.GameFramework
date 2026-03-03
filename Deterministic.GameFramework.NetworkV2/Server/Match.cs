using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.NetworkV2.Server;

/// <summary>
/// Represents an active game session on the server.
/// Wraps the CoreV2 GameLoop and GlobalState.
/// </summary>
public class Match : IDisposable
{
    public System.Guid Id { get; }
    public GlobalState State { get; }
    public GameLoop Loop { get; }
    public Dispatcher Dispatcher { get; }
    public ActionScheduler Scheduler { get; }
    
    private readonly List<System.Guid> _players = new();
    public IReadOnlyList<System.Guid> Players => _players;

    public event Action<System.Guid>? OnPlayerJoined;
    public event Action<System.Guid>? OnPlayerLeft;

    public Match(System.Guid id, GlobalState state, GameLoop loop, Dispatcher dispatcher, ActionScheduler scheduler)
    {
        Id = id;
        State = state;
        Loop = loop;
        Dispatcher = dispatcher;
        Scheduler = scheduler;
    }

    public void AddPlayer(System.Guid playerId)
    {
        if (!_players.Contains(playerId))
        {
            _players.Add(playerId);
            OnPlayerJoined?.Invoke(playerId);
        }
    }

    public void RemovePlayer(System.Guid playerId)
    {
        if (_players.Remove(playerId))
        {
            OnPlayerLeft?.Invoke(playerId);
        }
    }

    public void Dispose()
    {
        Loop.Stop();
    }
}
