namespace Deterministic.GameFramework.Utils.Logging;

public class ConsoleLogger : ILogger
{
    public ConsoleLogger() 
    {
        ILogger.SetLogger(this);
    }
    
    public void _Log(string message) => Console.WriteLine($"[Console] {message}");
    public void _LogWarning(string message) => Console.WriteLine($"[Console Warning] {message}");
    public void _LogError(string message) => Console.WriteLine($"[Console Error] {message}");
    public void _LogDebug(string message) => Console.WriteLine($"[Console Debug] {message}");
}