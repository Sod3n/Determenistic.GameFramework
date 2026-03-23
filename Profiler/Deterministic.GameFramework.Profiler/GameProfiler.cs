using System.Collections.Generic;
using System.Reflection;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Profiler;

/// <summary>
/// Lightweight profiler for deterministic game framework. Core is completely unaware of it.
///
/// One-line setup (after systems are registered):
///   GameProfiler.Enable(game);
///
/// Manual setup:
///   var profiler = new GameProfiler();
///   simulation.SystemRunner.EnableSystem(profiler.Profile(new CowSystem()));
///   profiler.Attach(simulation);
/// </summary>
public sealed class GameProfiler : IDisposable
{
    private static GameProfiler? _instance;

    private readonly List<ProfiledSystem> _profiledSystems = new();
    private GameSimulation? _simulation;
    private bool _attached;

    private long _prevGcMemory;
    private int _ticksSinceReport;

    /// <summary>Rolling window size for statistics (number of ticks).</summary>
    public int WindowSize { get; }

    /// <summary>A spike is when a system takes more than this multiplier x its rolling average.</summary>
    public double SpikeThreshold { get; set; } = 3.0;

    /// <summary>Fire report every N ticks. 0 = manual only.</summary>
    public int ReportInterval { get; set; } = 300; // every 5 seconds at 60hz

    /// <summary>Also log spikes immediately when detected (not just at report intervals).</summary>
    public bool LogSpikesImmediately { get; set; } = true;

    /// <summary>Minimum average time (seconds) before spike detection kicks in. Prevents noise on very fast systems.</summary>
    public double SpikeMinThresholdSeconds { get; set; } = 0.0001; // 0.1ms

    /// <summary>GC memory growth threshold (bytes) per report interval to flag as potential leak.</summary>
    public long MemoryGrowthWarningBytes { get; set; } = 1024 * 1024; // 1 MB

    /// <summary>Fired when a periodic report is generated.</summary>
    public event Action<ProfilerSnapshot>? OnReport;

    /// <summary>Fired immediately when a spike is detected on any system.</summary>
    public event Action<string, double, double>? OnSpike; // systemName, actualSeconds, avgSeconds

    /// <summary>The current active profiler instance (set by Enable).</summary>
    public static GameProfiler? Instance => _instance;

    public GameProfiler(int windowSize = 300)
    {
        WindowSize = windowSize;
    }

    /// <summary>
    /// One-line setup: wraps all currently registered systems with profiling and attaches to the simulation.
    /// Uses reflection to read SystemRunner's private _systems list and replace entries in-place.
    /// Call this AFTER all systems have been registered (e.g. after SceneManager.LoadScene or ServiceLocator.Register).
    /// </summary>
    public static GameProfiler Enable(Game game) => Enable(game.Simulation);

    /// <summary>
    /// One-line setup via GameLoop.
    /// </summary>
    public static GameProfiler Enable(GameLoop loop) => Enable(loop.Simulation);

    /// <summary>
    /// One-line setup via GameSimulation.
    /// </summary>
    public static GameProfiler Enable(GameSimulation simulation)
    {
        // Dispose previous instance if re-enabling
        _instance?.Dispose();

        var profiler = new GameProfiler();
        profiler.WrapExistingSystems(simulation.SystemRunner);
        profiler.Attach(simulation);

        _instance = profiler;
        ILogger.Log("[Profiler] Enabled. Profiling " + profiler._profiledSystems.Count + " systems.");
        return profiler;
    }

    /// <summary>
    /// Disable profiling: unwrap all systems back to originals and detach.
    /// </summary>
    public static void Disable()
    {
        _instance?.Dispose();
        _instance = null;
    }

    /// <summary>
    /// Uses reflection to replace all systems in SystemRunner with profiled wrappers.
    /// </summary>
    private void WrapExistingSystems(SystemRunner runner)
    {
        var field = typeof(SystemRunner).GetField("_systems", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            ILogger.LogWarning("[Profiler] Could not find SystemRunner._systems field. Per-system profiling unavailable.");
            return;
        }

        var systems = (List<ISystem>?)field.GetValue(runner);
        if (systems == null) return;

        for (int i = 0; i < systems.Count; i++)
        {
            // Don't double-wrap
            if (systems[i] is ProfiledSystem) continue;

            var profiled = new ProfiledSystem(systems[i], WindowSize);
            _profiledSystems.Add(profiled);
            systems[i] = profiled;
        }
    }

    /// <summary>
    /// Unwrap all profiled systems back to their originals in SystemRunner.
    /// </summary>
    private void UnwrapSystems()
    {
        if (_simulation == null) return;

        var field = typeof(SystemRunner).GetField("_systems", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null) return;

        var systems = (List<ISystem>?)field.GetValue(_simulation.SystemRunner);
        if (systems == null) return;

        for (int i = 0; i < systems.Count; i++)
        {
            if (systems[i] is ProfiledSystem profiled)
            {
                systems[i] = profiled.Inner;
            }
        }

        _profiledSystems.Clear();
    }

    /// <summary>
    /// Wrap a system with profiling. Returns the wrapped system (register this one with SystemRunner).
    /// </summary>
    public ProfiledSystem Profile(ISystem system)
    {
        var profiled = new ProfiledSystem(system, WindowSize);
        _profiledSystems.Add(profiled);
        return profiled;
    }

    /// <summary>
    /// Wrap multiple systems.
    /// </summary>
    public IEnumerable<ProfiledSystem> Profile(IEnumerable<ISystem> systems)
    {
        foreach (var system in systems)
            yield return Profile(system);
    }

    /// <summary>
    /// Attach to a GameSimulation to receive tick callbacks and track entity counts / GC memory.
    /// </summary>
    public void Attach(GameSimulation simulation)
    {
        if (_attached) Detach();

        _simulation = simulation;
        _simulation.OnTick += OnTickHandler;
        _prevGcMemory = GC.GetTotalMemory(false);
        _ticksSinceReport = 0;
        _attached = true;
    }

    /// <summary>
    /// Attach via GameLoop (convenience).
    /// </summary>
    public void Attach(GameLoop loop) => Attach(loop.Simulation);

    /// <summary>
    /// Attach via Game (convenience).
    /// </summary>
    public void Attach(Game game) => Attach(game.Simulation);

    /// <summary>
    /// Detach from the simulation and unwrap systems.
    /// </summary>
    public void Detach()
    {
        if (_simulation != null)
        {
            _simulation.OnTick -= OnTickHandler;
            UnwrapSystems();
            _simulation = null;
        }
        _attached = false;
    }

    private void OnTickHandler()
    {
        // Check for immediate spikes
        if (LogSpikesImmediately)
        {
            foreach (var sys in _profiledSystems)
            {
                if (sys.Stats.Count < 10) continue; // need warmup
                double avg = sys.Stats.Average;
                if (avg < SpikeMinThresholdSeconds) continue;

                if (sys.LastElapsedSeconds > avg * SpikeThreshold)
                {
                    if (OnSpike != null)
                    {
                        OnSpike.Invoke(sys.Name, sys.LastElapsedSeconds, avg);
                    }
                    else
                    {
                        ILogger.LogWarning($"[Profiler] SPIKE: {sys.Name} took {sys.LastElapsedSeconds * 1000:F3}ms (avg {avg * 1000:F3}ms, {sys.LastElapsedSeconds / avg:F1}x)");
                    }
                }
            }
        }

        // Periodic report
        if (ReportInterval > 0)
        {
            _ticksSinceReport++;
            if (_ticksSinceReport >= ReportInterval)
            {
                var snapshot = TakeSnapshot();
                if (OnReport != null)
                {
                    OnReport.Invoke(snapshot);
                }
                else
                {
                    ILogger.Log(snapshot.ToString());
                }

                _ticksSinceReport = 0;
            }
        }
    }

    /// <summary>
    /// Take a snapshot of current profiler state. Can be called manually at any time.
    /// </summary>
    public ProfilerSnapshot TakeSnapshot()
    {
        long currentGcMemory = GC.GetTotalMemory(false);
        long gcDelta = currentGcMemory - _prevGcMemory;
        _prevGcMemory = currentGcMemory;

        double totalSystems = 0;
        var spikes = new List<string>();
        var systemSnapshots = new ProfilerSnapshot.SystemSnapshot[_profiledSystems.Count];

        for (int i = 0; i < _profiledSystems.Count; i++)
        {
            var sys = _profiledSystems[i];
            totalSystems += sys.LastElapsedSeconds;

            double avg = sys.Stats.Average;
            bool isSpike = sys.Stats.Count >= 10
                           && avg >= SpikeMinThresholdSeconds
                           && sys.LastElapsedSeconds > avg * SpikeThreshold;

            if (isSpike) spikes.Add(sys.Name);

            systemSnapshots[i] = new ProfilerSnapshot.SystemSnapshot
            {
                Name = sys.Name,
                LastSeconds = sys.LastElapsedSeconds,
                AvgSeconds = avg,
                MaxSeconds = sys.Stats.Max,
                MinSeconds = sys.Stats.Min,
                IsSpike = isSpike
            };
        }

        if (gcDelta > MemoryGrowthWarningBytes)
        {
            spikes.Add($"GC_MEMORY_GROWTH({gcDelta / 1024}KB)");
        }

        int entityCount = _simulation?.State.NextEntityId ?? 0;

        return new ProfilerSnapshot
        {
            Tick = _simulation?.CurrentTick ?? 0,
            EntityCount = entityCount,
            GcMemoryBytes = currentGcMemory,
            GcMemoryDeltaBytes = gcDelta,
            Systems = systemSnapshots,
            TotalSystemsSeconds = totalSystems,
            Spikes = spikes
        };
    }

    /// <summary>
    /// Reset all rolling statistics.
    /// </summary>
    public void Reset()
    {
        foreach (var sys in _profiledSystems)
            sys.Stats.Reset();
        _prevGcMemory = GC.GetTotalMemory(false);
        _ticksSinceReport = 0;
    }

    public void Dispose()
    {
        Detach();
        ILogger.Log("[Profiler] Disabled.");
    }
}
