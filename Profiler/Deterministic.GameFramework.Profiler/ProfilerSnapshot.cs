namespace Deterministic.GameFramework.Profiler;

/// <summary>
/// Immutable point-in-time snapshot of profiler data.
/// </summary>
public sealed class ProfilerSnapshot
{
    public long Tick { get; init; }
    public int EntityCount { get; init; }
    public long GcMemoryBytes { get; init; }
    public long GcMemoryDeltaBytes { get; init; }
    public SystemSnapshot[] Systems { get; init; } = Array.Empty<SystemSnapshot>();
    public double TotalSystemsSeconds { get; init; }
    public List<string> Spikes { get; init; } = new();

    public sealed class SystemSnapshot
    {
        public string Name { get; init; } = "";
        public double LastSeconds { get; init; }
        public double AvgSeconds { get; init; }
        public double MaxSeconds { get; init; }
        public double MinSeconds { get; init; }
        public bool IsSpike { get; init; }
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Profiler] Tick {Tick} — Systems total: {TotalSystemsSeconds * 1000:F3}ms");

        foreach (var sys in Systems)
        {
            var spike = sys.IsSpike ? " << SPIKE" : "";
            sb.AppendLine($"  {sys.Name,-30} {sys.LastSeconds * 1000,8:F3}ms  (avg {sys.AvgSeconds * 1000:F3}ms, max {sys.MaxSeconds * 1000:F3}ms){spike}");
        }

        sb.AppendLine($"  Entities: {EntityCount}  GC Memory: {GcMemoryBytes / 1024.0:F1} KB (delta: {(GcMemoryDeltaBytes >= 0 ? "+" : "")}{GcMemoryDeltaBytes / 1024.0:F1} KB)");

        if (Spikes.Count > 0)
        {
            sb.AppendLine($"  Spikes detected: {string.Join(", ", Spikes)}");
        }

        return sb.ToString();
    }
}
