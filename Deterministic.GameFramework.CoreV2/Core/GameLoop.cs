using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public class GameLoop : IDisposable
{
    private readonly GlobalState _state;
    private readonly Dispatcher _dispatcher;
    private readonly ActionScheduler _scheduler;
    private readonly SystemRunner _systemRunner;

    public Dispatcher Dispatcher => _dispatcher;

    private bool _isRunning;
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;
    
    private int _tickRate = 60;
    private float _fixedDeltaTime = 1f / 60f;
    private float _accumulator;
    
    private const int MaxTicksPerFrame = 5;

    public int TickDelay { get; set; }
    public float FixedDeltaTime => _fixedDeltaTime;
    public long CurrentTick { get; private set; }
    public int TickRate => _tickRate;
    public bool IsResimulating { get; private set; }
    
    public event Action? OnTick;
    public event Action? OnRollbackFailed;

    private readonly StateHistory _history;
    public StateHistory History => _history;
    public GlobalState State => _state;
    public SystemRunner SystemRunner => _systemRunner;

    public GameLoop(GlobalState state, Dispatcher dispatcher, ActionScheduler scheduler)
    {
        _state = state;
        _dispatcher = dispatcher;
        _scheduler = scheduler;
        _systemRunner = new SystemRunner();
        // Dispatcher is now called explicitly in Tick()
        _history = new StateHistory(300); // 5 seconds of history @ 60hz (increased from 60)
        
        _state.GameLoop = this;
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
                Console.WriteLine($"[GameLoop] CRITICAL ERROR in frame update: {ex.Message}");
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
        var denseId = ComponentId<TAction>.DenseId;
        _scheduler.Schedule(action, denseId, target, CurrentTick);
    }
    
    public void ScheduleOnTick<TAction>(long tick, TAction action, Entity target) where TAction : struct, IAction
    {
        var denseId = ComponentId<TAction>.DenseId;
        _scheduler.Schedule(action, denseId, target, tick);
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
        CurrentTick = tick;
        // Clear accumulator to avoid immediate catch-up ticks
        _accumulator = 0;
        // Reset history to avoid trying to rollback to invalid past
        // Actually, we should probably clear history if we jump ticks significantly
        // For now, let's just update the tick.
    }

    private void Tick()
    {
        // 0. Check for Rollback
        long dirtyTick = _scheduler.EarliestDirtyTick;
        if (dirtyTick < CurrentTick)
        {
            // Rollback required!
            long originalTick = CurrentTick;
            long restoreTick = dirtyTick - 1;
            
            // Try to find snapshot
            if (_history.Retrieve(restoreTick, _state))
            {
                Console.WriteLine($"[Rollback] Rolling back from {CurrentTick} to {restoreTick} (Input at {dirtyTick})");
                
                // Truncate the "False Future"
                _history.DiscardFuture(restoreTick);
                
                CurrentTick = restoreTick;
                
                // RESIMULATION LOOP (Catch up to where we were)
                IsResimulating = true;
                while (CurrentTick < originalTick)
                {
                    // 1. Apply Actions (Add Components)
                    _scheduler.ExecuteActions(CurrentTick, _state, _dispatcher);

                    // 1.5 Process Actions
                    _dispatcher.Update(_state);

                    // 2. Run Systems (Process Logic & Action Components)
                    _systemRunner.Update(_state);
                    
                    // Note: We might want to suppress OnTick (Render/Audio) during resimulation
                    // But for logic listeners (like the test logger), we keep it or handle it.
                    // For this PoC, we'll invoke it but maybe listeners should check 'IsResimulating' flag if we added one.
                    try { OnTick?.Invoke(); } catch {}
                    
                    CurrentTick++;
                    _history.Store(CurrentTick, _state);
                }
                IsResimulating = false;
            }
            else
            {
                Console.WriteLine($"[Rollback] Failed to restore state for tick {restoreTick}. History range: {_history.GetOldestTick()}-{_history.GetLatestTick()}. Requesting sync.");
                OnRollbackFailed?.Invoke();
                return; // Abort tick, wait for sync
            }
        }

        // 1. Simulate (Normal Step)
        // 1.5 Apply Actions (Add Components)
        _scheduler.ExecuteActions(CurrentTick, _state, _dispatcher);

        // 1.6 Process Actions
        _dispatcher.Update(_state);

        // 1.7 Run Systems (Process Logic & Action Components)
        _systemRunner.Update(_state);
        
        try
        {
            OnTick?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameLoop] Error in OnTick listener: {ex.Message}");
        }
        
        CurrentTick++;
        
        // 2. Save State to History
        _history.Store(CurrentTick, _state);
        
        // 3. Prune Old Data
        long oldestTick = _history.GetOldestTick();
        if (oldestTick > 0)
        {
            _scheduler.PruneHistory(oldestTick);
        }
        
        // 4. Clear Dirty State
        _state.ClearDirty();
    }
}
