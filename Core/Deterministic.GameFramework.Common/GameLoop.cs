using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Common;

public class GameLoop : IDisposable, IGameTime
{
    public GameSimulation Simulation { get; }

    public GameLoop(GameSimulation simulation)
    {
        Simulation = simulation;
        RegisterDependencies();
    }

    public GameLoop(EntityWorld state, Dispatcher dispatcher, ActionScheduler scheduler)
        : this(new GameSimulation(state, dispatcher, scheduler))
    {
    }

    private void RegisterDependencies()
    {
        Simulation.State.SetCustomData<IGameTime>(this);
        
        if (Simulation.Dispatcher != null)
        {
            if (Simulation.Dispatcher.ActionDispatcher != null)
            {
                Simulation.State.SetCustomData<IActionDispatcher>(Simulation.Dispatcher.ActionDispatcher);
            }
            else
            {
                var actionDispatcher = new GameLoopActionDispatcher(Simulation);
                Simulation.Dispatcher.ActionDispatcher = actionDispatcher;
                Simulation.State.SetCustomData<IActionDispatcher>(actionDispatcher);
            }
        }
    }

    // Expose needed properties for compatibility / access

    private bool _isRunning;
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;
    
    private int _tickRate = 60;
    private float _fixedDeltaTime = 1f / 60f;
    private float _accumulator;
    
    private const int MaxTicksPerFrame = 5;

    public int TickDelay { get; set; }
    public float FixedDeltaTime => _fixedDeltaTime;
    
    public long CurrentTick => Simulation.CurrentTick;
    public int TickRate => _tickRate;
    public bool IsResimulating => Simulation.IsResimulating;
    
    // Delegate events
    public event Action? OnTick
    {
        add => Simulation.OnTick += value;
        remove => Simulation.OnTick -= value;
    }
    
    public event Action? OnRollbackFailed
    {
        add => Simulation.OnRollbackFailed += value;
        remove => Simulation.OnRollbackFailed -= value;
    }

    public void Dispose()
    {
        Stop();
    }

    public void SetTickRate(int tickRate)
    {
        _tickRate = tickRate;
        _fixedDeltaTime = 1f / tickRate;
    }

    public Task Start()
    {
        return Task.Run(() =>
        {
            try
            {
                InitializeLoop();
                RunLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameLoop] FATAL ERROR - Loop crashed: {ex.Message}");
                _isRunning = false;
                throw;
            }
        });
    }

    private void InitializeLoop()
    {
        _isRunning = true;
        _fixedDeltaTime = 1f / _tickRate;
        _accumulator = -(TickDelay * _fixedDeltaTime);
        // CurrentTick = 0; // Don't reset if we want to start from a synced tick
        
        // Store initial state (State at beginning of Tick 0)
        Simulation.History.Store(Simulation.CurrentTick, Simulation.State);
        
        _stopwatch.Start();
        _lastFrameTicks = _stopwatch.ElapsedTicks;
    }

    private void RunLoop()
    {
        int targetFrameTimeMs = 1000 / _tickRate;
        
        while (_isRunning)
        {
            try
            {
                ProcessFrame(targetFrameTimeMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameLoop] CRITICAL ERROR in frame update: {ex.ToString()}");
            }
        }
    }

    private void ProcessFrame(int targetFrameTimeMs)
    {
        long currentTicks = _stopwatch.ElapsedTicks;
        float elapsed = GetElapsedSecondsAndUpdateLastFrameTicks(currentTicks);
        
        _accumulator += elapsed;
        
        ProcessFixedTicks();
        
        PreventAccumulatorSpiralOfDeath();
        
        SleepUntilNextFrame(currentTicks, targetFrameTimeMs);
    }

    private float GetElapsedSecondsAndUpdateLastFrameTicks(long currentTicks)
    {
        long deltaTicks = currentTicks - _lastFrameTicks;
        _lastFrameTicks = currentTicks;
        return (float)deltaTicks / Stopwatch.Frequency;
    }

    private void ProcessFixedTicks()
    {
        int ticksThisFrame = 0;
        while (_accumulator >= _fixedDeltaTime && ticksThisFrame < MaxTicksPerFrame)
        {
            Tick();
            _accumulator -= _fixedDeltaTime;
            // CurrentTick is handled inside Tick()
            ticksThisFrame++;
        }
    }

    private void PreventAccumulatorSpiralOfDeath()
    {
        if (_accumulator > _fixedDeltaTime)
        {
            _accumulator = 0f;
        }
    }

    private void SleepUntilNextFrame(long frameStartTicks, int targetFrameTimeMs)
    {
        long frameEndTicks = _stopwatch.ElapsedTicks;
        long frameDurationTicks = frameEndTicks - frameStartTicks;
        int frameDurationMs = (int)(frameDurationTicks * 1000 / Stopwatch.Frequency);
        
        int sleepTime = Math.Max(0, targetFrameTimeMs - frameDurationMs);
        if (sleepTime > 0)
        {
            System.Threading.Thread.Sleep(sleepTime);
        }
    }

    public void Stop() => _isRunning = false;
    
    public void AdvanceToTick(long targetTick)
    {
        while (CurrentTick < targetTick)
        {
            Tick();
            // CurrentTick is handled inside Tick()
        }
    }

    public void Schedule<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        Simulation.Schedule(action, target);
    }
    
    public void ScheduleOnTick<TAction>(long tick, TAction action, Entity target) where TAction : struct, IAction
    {
        Simulation.ScheduleOnTick(tick, action, target);
    }

    /// <summary>
    /// Manually run a single tick. Useful for testing or manual driving.
    /// </summary>
    public void RunSingleTick()
    {
        Tick();
    }

    public void ForceSetTick(long tick)
    {
        Simulation.ForceSetTick(tick);
        // Clear accumulator to avoid immediate catch-up ticks
        _accumulator = 0;
    }

    private void Tick()
    {
        Simulation.Tick();
    }
}

