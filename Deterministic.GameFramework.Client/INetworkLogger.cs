namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Abstraction for logging in the network layer.
    /// Allows the network code to be used across different game engines (Godot, Unity, etc.)
    /// </summary>
    public interface INetworkLogger
    {
        /// <summary>
        /// Log an informational message.
        /// </summary>
        void Log(string message);
        
        /// <summary>
        /// Log an error message.
        /// </summary>
        void LogError(string message);
    }
}
