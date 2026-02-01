using System;
using System.Collections.Generic;
using R3;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Server;

namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Core network client for connecting to the server via SignalR.
    /// Handles connection lifecycle and action synchronization with multiple channels.
    /// Engine-agnostic - works with Godot, Unity, MonoGame, etc.
    /// </summary>
    public class NetworkClient
    {
        public SignalR SignalR { get; private set; }
        
        // Events
        public Subject<Unit> OnConnected { get; } = new();
        public Subject<Unit> OnDisconnected { get; } = new();
        
        // Time sync events (server UTC ticks)
        public Subject<long> OnTimeSync { get; } = new();
        
        // Action queues per thread (automatically initialized for all NetworkThread values)
        public Dictionary<NetworkThread, Queue<INetworkAction>> ActionsByThread { get; }
        
        // Network statistics
        public long TotalBytesReceived { get; private set; }
        
        public NetworkClient(string hubUrl)
        {
            // Automatically create queues for all registered NetworkThread values
            ActionsByThread = new Dictionary<NetworkThread, Queue<INetworkAction>>();
            foreach (NetworkThread thread in NetworkThread.GetAll())
            {
                ActionsByThread[thread] = new Queue<INetworkAction>();
            }
            
            SignalR = new SignalR();
            SignalR.Init(hubUrl);

            // Listen for synced actions from server
            SignalR.On<string>("SyncActions", (actionsJson) =>
            {
                try
                {
                    var bytes = System.Text.Encoding.UTF8.GetByteCount(actionsJson);
                    TotalBytesReceived += bytes;
                    
                    NetworkLogger.Log($"[Network] Received {bytes / 1024.0:F2} KiB (Total: {TotalBytesReceived / 1024.0:F0} KiB)");
                    
                    // Deserialize actions from JSON
                    var actions = JsonSerializer.FromJson<List<INetworkAction>>(actionsJson);
                    if (actions != null)
                    {
                        foreach (var action in actions)
                        {
                            // Route to appropriate thread queue
                            if (ActionsByThread.TryGetValue(action.Thread, out var queue))
                            {
                                queue.Enqueue(action);
                            }
                            else
                            {
                                // Default to Main thread if unknown
                                ActionsByThread[NetworkThread.Main].Enqueue(action);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    NetworkLogger.LogError($"[Network] Error deserializing actions: {e.Message}");
                }
            });

            // Listen for single action (e.g., initial sync)
            SignalR.On<string>("ReceiveAction", (actionJson) =>
            {
                try
                {
                    NetworkLogger.Log($"[Network] Received single action");
                    
                    // Deserialize single action from JSON
                    var action = JsonSerializer.FromJson<INetworkAction>(actionJson);
                    if (action != null)
                    {
                        // Route to appropriate thread queue
                        if (ActionsByThread.TryGetValue(action.Thread, out var queue))
                        {
                            queue.Enqueue(action);
                        }
                        else
                        {
                            // Default to Main thread if unknown
                            ActionsByThread[NetworkThread.Main].Enqueue(action);
                        }
                    }
                }
                catch (Exception e)
                {
                    NetworkLogger.LogError($"[Network] Error deserializing action: {e.Message}");
                }
            });
            
            // Listen for pong response (latency only)
            SignalR.On<long>("Pong", (time) =>
            {
                var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - time;
                NetworkLogger.Log($"[Network] Pong! Latency: {latency}ms");
            });

            // Listen for time sync response (server UTC ticks)
            SignalR.On<long>("TimeSync", serverTicks =>
            {
                OnTimeSync.OnNext(serverTicks);
            });

            // Connection lifecycle
            SignalR.ConnectionStarted += (_, _) =>
            {
                NetworkLogger.Log("[Network] Connected to server!");
                OnConnected.OnNext(Unit.Default);
            };

            SignalR.ConnectionClosed += (_, _) =>
            {
                NetworkLogger.Log("[Network] Disconnected from server!");
                OnDisconnected.OnNext(Unit.Default);
            };

            SignalR.Connect();
        }

        /// <summary>
        /// Close the connection to the server.
        /// </summary>
        public void Close() => SignalR.Stop();
        
        /// <summary>
        /// Send a ping to the server to measure latency.
        /// </summary>
        public void Ping(long clientTime) => SignalR.Invoke("Ping", clientTime);

        /// <summary>
        /// Request a time sync from the server.
        /// clientTicks should be DateTime.UtcNow.Ticks at send time.
        /// </summary>
        public void RequestTimeSync(long clientTicks)
            => SignalR.Invoke("TimeSync", clientTicks);
        
        /// <summary>
        /// Invoke a method on the server hub.
        /// </summary>
        public void Invoke(string methodName, params object[] args)
        {
            switch (args.Length)
            {
                case 0: SignalR.Invoke(methodName); break;
                case 1: SignalR.Invoke(methodName, args[0]); break;
                case 2: SignalR.Invoke(methodName, args[0], args[1]); break;
                case 3: SignalR.Invoke(methodName, args[0], args[1], args[2]); break;
                case 4: SignalR.Invoke(methodName, args[0], args[1], args[2], args[3]); break;
                case 5: SignalR.Invoke(methodName, args[0], args[1], args[2], args[3], args[4]); break;
                default: throw new ArgumentException("Too many arguments. Maximum 5 supported.");
            }
        }
    }
}
