namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Static logger for the network layer.
    /// Call SetLogger() at application startup to configure logging for your engine.
    /// </summary>
    public static class NetworkLogger
    {
        private static INetworkLogger _instance;

        /// <summary>
        /// Set the logger implementation for the network layer.
        /// Must be called before using any network classes.
        /// </summary>
        /// <param name="logger">Logger implementation (e.g., GodotNetworkLogger for Godot)</param>
        public static void SetLogger(INetworkLogger logger)
        {
            _instance = logger;
        }

        /// <summary>
        /// Log an informational message.
        /// </summary>
        public static void Log(string message)
        {
            _instance?.Log(message);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public static void LogError(string message)
        {
            _instance?.LogError(message);
        }
    }
}
