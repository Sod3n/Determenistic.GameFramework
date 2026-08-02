namespace Deterministic.GameFramework.Utils.Logging;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4, // nothing gets through
}

/// <summary>
/// Static logging entry point. Callers use <see cref="Log"/>, <see cref="LogWarning"/>,
/// <see cref="LogError"/>. An early-exit on <see cref="MinLevel"/> avoids paying format-string
/// cost when the sink is silenced — critical on the server hot path where per-tick logs
/// can cost more than the sim itself (stdout's <c>SyncTextWriter</c> lock).
/// </summary>
public interface ILogger
{
    public static ILogger? Instance { get; set; }

    /// <summary>
    /// Messages strictly below this level are dropped before reaching the sink.
    /// Set to <see cref="LogLevel.None"/> on the VPS to silence everything (default from
    /// <c>LOG_LEVEL</c> env var via <see cref="ReadLevelFromEnvironment"/>).
    /// </summary>
    public static LogLevel MinLevel { get; set; } = LogLevel.Info;

    public static void SetLogger(ILogger logger) => Instance = logger;

    public static bool IsEnabled(LogLevel level) => level >= MinLevel && Instance != null;

    public void _Log(string message);
    public void _LogWarning(string message);
    public void _LogError(string message);
    public void _LogDebug(string message) => _Log(message);

    public static void LogDebug(string message)
    {
        if (MinLevel > LogLevel.Debug) return;
        Instance?._LogDebug(message);
    }

    public static void Log(string message)
    {
        if (MinLevel > LogLevel.Info) return;
        Instance?._Log(message);
    }

    public static void LogWarning(string message)
    {
        if (MinLevel > LogLevel.Warning) return;
        Instance?._LogWarning(message);
    }

    public static void LogError(string message)
    {
        if (MinLevel > LogLevel.Error) return;
        Instance?._LogError(message);
    }

    /// <summary>
    /// Reads <c>LOG_LEVEL</c> environment variable and applies it to <see cref="MinLevel"/>.
    /// Values: <c>Debug</c>, <c>Info</c>, <c>Warning</c>, <c>Error</c>, <c>None</c> (case-insensitive).
    /// Unknown / unset → no change (default stays <c>Info</c>).
    /// Call once at process startup.
    /// </summary>
    public static void ReadLevelFromEnvironment(string envVar = "LOG_LEVEL")
    {
        var v = System.Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrWhiteSpace(v) && System.Enum.TryParse<LogLevel>(v, ignoreCase: true, out var level))
            MinLevel = level;
    }
}
