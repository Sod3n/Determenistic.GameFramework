namespace Deterministic.GameFramework.Server;

/// <summary>
/// Network thread/channel types for organizing different types of messages.
/// Uses a registry pattern to allow extensibility without modifying core.
/// </summary>
public sealed class NetworkThread : IEquatable<NetworkThread>
{
    private static readonly Dictionary<int, NetworkThread> _registry = new();
    private static readonly object _lock = new();
    
    public int Id { get; }
    public string Name { get; }
    
    private NetworkThread(int id, string name)
    {
        Id = id;
        Name = name;
    }
    
    /// <summary>
    /// Register a new network thread type. Thread IDs must be unique.
    /// </summary>
    /// <param name="id">Unique identifier for the thread</param>
    /// <param name="name">Descriptive name for the thread</param>
    /// <returns>The registered NetworkThread instance</returns>
    public static NetworkThread Register(int id, string name)
    {
        lock (_lock)
        {
            if (_registry.ContainsKey(id))
                throw new InvalidOperationException($"NetworkThread with ID {id} is already registered.");
            
            var thread = new NetworkThread(id, name);
            _registry[id] = thread;
            return thread;
        }
    }
    
    /// <summary>
    /// Get a registered thread by ID. Returns null if not found.
    /// </summary>
    public static NetworkThread? GetById(int id)
    {
        lock (_lock)
        {
            return _registry.TryGetValue(id, out var thread) ? thread : null;
        }
    }
    
    /// <summary>
    /// Get all registered network threads.
    /// </summary>
    public static IEnumerable<NetworkThread> GetAll()
    {
        lock (_lock)
        {
            return _registry.Values.ToList();
        }
    }
    
    // Built-in thread types
    public static readonly NetworkThread Main = Register(0, "Main");
    public static readonly NetworkThread PingPong = Register(1, "PingPong");
    public static readonly NetworkThread Chat = Register(2, "Chat");
    
    // Equality implementation
    public bool Equals(NetworkThread? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }
    
    public override bool Equals(object? obj) => Equals(obj as NetworkThread);
    public override int GetHashCode() => Id;
    public override string ToString() => $"{Name} (ID: {Id})";
    
    public static bool operator ==(NetworkThread? left, NetworkThread? right) => Equals(left, right);
    public static bool operator !=(NetworkThread? left, NetworkThread? right) => !Equals(left, right);
}