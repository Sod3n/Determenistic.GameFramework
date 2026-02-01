using System;
using R3;

namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Provides server-synchronized UTC time for the client.
    /// Uses NetworkClient time sync hooks under the hood.
    /// Engine-agnostic - works with Godot, Unity, MonoGame, etc.
    /// </summary>
    public class NetworkTime : IDisposable
    {
        private readonly NetworkClient _networkClient;
        private readonly IDisposable _timeSyncSubscription;
        private readonly IDisposable _periodicSyncSubscription;
        private long _lastRequestTicks;
        
        /// <summary>
        /// Current offset between server and client clocks in ticks.
        /// serverUtcTicks = DateTime.UtcNow.Ticks + TimeOffsetTicks
        /// </summary>
        public long TimeOffsetTicks { get; private set; }
        
        /// <summary>
        /// True once at least one successful sync has completed.
        /// </summary>
        public bool IsSynced { get; private set; }
        
        /// <summary>
        /// Emits whenever time is (re)sycned. Value is new offset in ticks.
        /// </summary>
        public Subject<long> OnTimeSynced { get; } = new();
        
        /// <summary>
        /// Server-synchronized UTC time.
        /// </summary>
        public DateTime UtcNow => new DateTime(DateTime.UtcNow.Ticks + TimeOffsetTicks, DateTimeKind.Utc);
        
        /// <summary>
        /// Create NetworkTime helper and start periodic synchronization.
        /// </summary>
        /// <param name="networkClient">The underlying NetworkClient.</param>
        /// <param name="syncInterval">How often to resync. Default: 30 seconds.</param>
        public NetworkTime(NetworkClient networkClient, TimeSpan? syncInterval = null)
        {
            _networkClient = networkClient;
            
            // Listen for server time responses
            _timeSyncSubscription = _networkClient.OnTimeSync.Subscribe(OnServerTime);            
            
            // Periodic sync
            var interval = syncInterval ?? TimeSpan.FromSeconds(30);
            _periodicSyncSubscription = Observable
                .Interval(interval)
                .Subscribe(_ => SyncNow());
            
            // Initial sync immediately
            SyncNow();
        }
        
        /// <summary>
        /// Force an immediate time synchronization.
        /// </summary>
        public void SyncNow()
        {
            _lastRequestTicks = DateTime.UtcNow.Ticks;
            _networkClient.RequestTimeSync(_lastRequestTicks);
        }
        
        private void OnServerTime(long serverTicks)
        {
            var t1 = DateTime.UtcNow.Ticks;
            var offset = serverTicks - ((_lastRequestTicks + t1) / 2);
            TimeOffsetTicks = offset;
            IsSynced = true;
            OnTimeSynced.OnNext(offset);
            NetworkLogger.Log($"[Time] Synced. Offset: {offset / (double)TimeSpan.TicksPerMillisecond:F2} ms");
        }
        
        public void Dispose()
        {
            _timeSyncSubscription.Dispose();
            _periodicSyncSubscription.Dispose();
            OnTimeSynced.OnCompleted();
        }
    }
}
