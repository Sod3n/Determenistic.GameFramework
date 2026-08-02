using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;
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
    private Float _fixedDeltaTimeFixed = (Float)1 / (Float)60; // Fixed64 division — no float intermediate
    private double _accumulator;
    
    private const int MaxTicksPerFrame = 10;

    // --- Network tick catch-up ---
    // When set, the loop runs extra ticks per frame to catch up to the server
    // without teleporting. Resets to -1 when caught up.
    private long _targetTick = -1;
    private const int CatchUpTicksPerFrame = 10; // Extra ticks per frame when catching up
    private const int MaxCatchUpTicksPerFrame = 30; // Hard cap during catch-up

    public int TickDelay { get; set; }
    public Float FixedDeltaTime => _fixedDeltaTimeFixed;
    public long CurrentTick => Simulation.CurrentTick;
    public int TickRate => _tickRate;
    public bool IsResimulating => Simulation.IsResimulating;
    public bool IsCatchingUp => _targetTick > 0 && Simulation.CurrentTick < _targetTick;

    /// <summary>
    /// Set by the network client when it knows the server's current tick.
    /// The loop will run extra ticks per frame to smoothly catch up.
    /// </summary>
    public long TargetTick
    {
        get => _targetTick;
        set => _targetTick = value;
    }
    
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
        _fixedDeltaTimeFixed = (Float)1 / (Float)tickRate;
    }

    public Task Start() => Start(name: null);

    /// <summary>
    /// Run the tick loop on a DEDICATED <see cref="Thread"/> (not a thread-pool task).
    /// Each match's loop is a long-running tight loop at 60 Hz — running them on the
    /// thread pool starves ASP.NET / HTTP handlers (including <c>CreateLobby</c>) once
    /// the number of active matches approaches the pool's available threads. A dedicated
    /// thread keeps the pool free for request handling and bounds concurrency to "number
    /// of cores" via the OS scheduler.
    /// </summary>
    public Task Start(string? name)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                InitializeLoop();
                RunLoop();
                tcs.TrySetResult(null);
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[GameLoop] FATAL ERROR - Loop crashed: {ex}");
                _isRunning = false;
                tcs.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = name ?? "GameLoop",
        };
        thread.Start();
        return tcs.Task;
    }

    private void InitializeLoop()
    {
        _isRunning = true;
        _fixedDeltaTime = 1.0 / _tickRate;
        _accumulator = 0;

        _stopwatch.Start();
        _lastFrameTicks = _stopwatch.ElapsedTicks;
    }

    private void RunLoop()
    {
        long targetTicksPerFrame = Stopwatch.Frequency / _tickRate;

        while (_isRunning)
        {
            long frameStartTicks = _stopwatch.ElapsedTicks;

            // 1. Accumulate real elapsed time
            double elapsed = GetElapsedSecondsAndUpdateLastFrameTicks(frameStartTicks);
            _accumulator += elapsed;

            // Cap accumulator to prevent sprint after GC/lag spikes.
            // Without this, a 500ms GC pause banks 30 ticks of debt that the client
            // burns through across several frames, permanently outrunning the server.
            double maxAccum = _fixedDeltaTime * MaxTicksPerFrame;
            if (_accumulator > maxAccum)
                _accumulator = maxAccum;

            // 2. Process ticks — re-check target every tick (it updates inside Tick via OnBeforeTick)
            int ticksProcessed = 0;
            while (_isRunning)
            {
                // Re-evaluate catch-up state each iteration (TargetTick changes mid-loop)
                long target = _targetTick;
                bool catchingUp = target > 0 && Simulation.CurrentTick < target;
                int maxTicks = catchingUp ? MaxCatchUpTicksPerFrame : MaxTicksPerFrame;

                // Need accumulator budget to run a tick
                if (_accumulator < _fixedDeltaTime)
                {
                    if (catchingUp)
                    {
                        // Inject virtual time to keep running without waiting for real time
                        _accumulator += _fixedDeltaTime;
                    }
                    else
                    {
                        break; // Normal mode: wait for real time
                    }
                }

                if (ticksProcessed >= maxTicks) break;

                Tick();
                _accumulator -= _fixedDeltaTime;
                ticksProcessed++;
            }

            // 4. Only wait for next frame if not catching up
            long target2 = _targetTick;
            if (target2 <= 0 || Simulation.CurrentTick >= target2)
            {
                YieldUntilNextFrame(frameStartTicks, targetTicksPerFrame);
            }
            else
            {
                Thread.Sleep(1);
            }
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
        long remaining = targetTicksPerFrame - (_stopwatch.ElapsedTicks - frameStartTicks);
        if (remaining <= 0) return;

        // Sleep for most of the remaining time (leave ~2ms for precision)
        int remainingMs = (int)(remaining * 1000 / Stopwatch.Frequency);
        if (remainingMs > 2)
            Thread.Sleep(remainingMs - 2);

        // Fine-spin only the last ~2ms
        while (_stopwatch.ElapsedTicks - frameStartTicks < targetTicksPerFrame)
            Thread.Sleep(1);
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