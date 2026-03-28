using System;
using System.Collections.Generic;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Network.Server;

/// <summary>
/// Represents an active game session on the server.
/// Wraps the CoreV2 GameLoop and GlobalState.
/// </summary>
public class Match : IDisposable
{
    public System.Guid Id { get; }
    public Game Game { get; }

    public EntityWorld State => Game.State;
    public GameLoop Loop => Game.Loop;
    public Dispatcher Dispatcher => Game.Dispatcher;
    public ActionScheduler Scheduler => Game.Scheduler;
    public SceneManager SceneManager => Game.SceneManager;
    
    private readonly List<System.Guid> _players = new();
    public IReadOnlyList<System.Guid> Players => _players;
    
    // Services attached to this match (e.g. StateVerificationService)
    private readonly List<IDisposable> _attachedServices = new();

    public event Action<System.Guid>? OnPlayerJoined;
    public event Action<System.Guid>? OnPlayerLeft;

    public Match(System.Guid id, Game game)
    {
        Id = id;
        Game = game;
    }
    
    public void AddService(IDisposable service)
    {
        _attachedServices.Add(service);
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
        
        foreach (var service in _attachedServices)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Match] Error disposing service {service.GetType().Name}: {ex}");
            }
        }
        _attachedServices.Clear();
    }
}
