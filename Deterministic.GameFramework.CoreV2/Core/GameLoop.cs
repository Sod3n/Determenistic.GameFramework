using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.CoreV2;

public class GameLoop
{
    private readonly GlobalState _state;
    private readonly Dispatcher _dispatcher;

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
    
    public event Action? OnTick;

    public GameLoop(GlobalState state, Dispatcher dispatcher)
    {
        _state = state;
        _dispatcher = dispatcher;
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
        CurrentTick = 0;
        
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
            CurrentTick++;
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
            CurrentTick++;
        }
    }

    public void Schedule<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        if (!_isRunning) return;
        _dispatcher.Schedule(action, target, CurrentTick);
    }
    
    public void ScheduleOnTick<TAction>(long tick, TAction action, Entity target) where TAction : struct, IAction
    {
        _dispatcher.Schedule(action, target, tick);
    }

    private void Tick()
    {
        _dispatcher.DrainScheduledActions(CurrentTick, _state);
        
        try
        {
            OnTick?.Invoke();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameLoop] Error in OnTick listener: {ex.Message}");
        }
    }
}
