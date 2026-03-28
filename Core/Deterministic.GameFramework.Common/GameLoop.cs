using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Common;

public class GameLoop : IDisposable, IGameTime
{
    public GameSimulation Simulation { get; }

    private bool _isRunning;
    private readonly Stopwatch _stopwatch = new();
    private long _lastFrameTicks;
    
    // Use double for precision in accumulator math
    private int _tickRate = 60;
    private double _fixedDeltaTime = 1.0 / 60.0;
    private double _accumulator;
    
    // Increased safety cap to allow faster catch-up if needed
    private const int MaxTicksPerFrame = 10; 

    public int TickDelay { get; set; }
    public float FixedDeltaTime => (float)_fixedDeltaTime;
    public long CurrentTick => Simulation.CurrentTick;
    public int TickRate => _tickRate;
    public bool IsResimulating => Simulation.IsResimulating;
    
    // Delegate events
    public event Action? OnBeforeTick
    {
        add => Simulation.OnBeforeTick += value;
        remove => Simulation.OnBeforeTick -= value;
    }

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

    public void SetTickRate(int tickRate)
    {
        _tickRate = tickRate;
        _fixedDeltaTime = 1.0 / tickRate;
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
                ILogger.LogError($"[GameLoop] FATAL ERROR - Loop crashed: {ex}");
                _isRunning = false;
                throw;
            }
        });
    }

    private void InitializeLoop()
    {
        _isRunning = true;
        _fixedDeltaTime = 1.0 / _tickRate;
        _accumulator = 0; 
        
        // Store initial state (State at beginning of Tick 0)
        Simulation.History.Store(Simulation.CurrentTick, Simulation.State);
        
        _stopwatch.Start();
        _lastFrameTicks = _stopwatch.ElapsedTicks;
    }

    private void RunLoop()
    {
        // Target time for one frame in ticks (Stopwatch frequency dependent)
        long targetTicksPerFrame = Stopwatch.Frequency / _tickRate;
        
        while (_isRunning)
        {
            long frameStartTicks = _stopwatch.ElapsedTicks;
            
            // 1. Calculate elapsed seconds with double precision
            double elapsed = GetElapsedSecondsAndUpdateLastFrameTicks(frameStartTicks);
            _accumulator += elapsed;

            // 2. Process Fixed Ticks
            // We run as many ticks as needed to clear the accumulator, up to a safety cap
            int ticksProcessed = 0;
            while (_accumulator >= _fixedDeltaTime && ticksProcessed < MaxTicksPerFrame)
            {
                Tick();
                _accumulator -= _fixedDeltaTime;
                ticksProcessed++;
            }

            // 3. Prevent "Spiral of Death"
            // Only wipe if we are more than 1 second behind real-time
            if (_accumulator > 1.0)
            {
                ILogger.LogWarning("[GameLoop] Performance Warning: Resetting accumulator (Lag > 1s)");
                _accumulator = 0;
            }

            // 4. Precision Wait
            YieldUntilNextFrame(frameStartTicks, targetTicksPerFrame);
        }
    }

    private double GetElapsedSecondsAndUpdateLastFrameTicks(long currentTicks)
    {
        long deltaTicks = currentTicks - _lastFrameTicks;
        _lastFrameTicks = currentTicks;
        return (double)deltaTicks / Stopwatch.Frequency;
    }

    private void YieldUntilNextFrame(long frameStartTicks, long targetTicksPerFrame)
    {
        // Thread-safe spin wait that allows other threads to work
        while (_stopwatch.ElapsedTicks - frameStartTicks < targetTicksPerFrame)
        {
            Thread.Yield();
        }
    }

    public void Stop() => _isRunning = false;
    
    public void Dispose() => Stop();

    public void AdvanceToTick(long targetTick)
    {
        while (CurrentTick < targetTick)
        {
            Tick();
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

    public void RunSingleTick() => Tick();

    public void ForceSetTick(long tick)
    {
        Simulation.ForceSetTick(tick);
        _accumulator = 0;
    }

    private void Tick() => Simulation.Tick();
}