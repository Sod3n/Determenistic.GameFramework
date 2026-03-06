namespace Deterministic.GameFramework.CoreV2.Logging;

public interface ILogger
{
    public static ILogger? Instance { get; set; }
    public static void SetLogger(ILogger logger) => Instance = logger;
    
    public void _Log(string message);
    public void _LogWarning(string message);
    public void _LogError(string message);
    
    public static void Log(string message) => Instance?._Log(message);
    public static void LogWarning(string message) => Instance?._LogWarning(message);
    public static void LogError(string message) => Instance?._LogError(message);
}