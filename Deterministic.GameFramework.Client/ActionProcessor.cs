using System;
using System.Collections.Generic;
using R3;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Client.Utils;

namespace Deterministic.GameFramework.Client
{
    /// <summary>
    /// Processes network actions from the queue with locking mechanism.
    /// Executes actions on local game state and emits events for UI updates.
    /// Engine-agnostic - works with Godot, Unity, MonoGame, etc.
    /// </summary>
    /// <typeparam name="TGameState">The type of game state (must inherit from BranchDomain)</typeparam>
    public class ActionProcessor<TGameState> where TGameState : BranchDomain
    {
        public NetworkClient NetworkClient { get; set; }
        
        // Events fired per thread (automatically initialized for all NetworkThread values)
        public readonly Dictionary<NetworkThread, Subject<INetworkAction>> OnActionByThread = new();
        
        // Legacy single event (uses Main thread)
        public Subject<INetworkAction> OnAction => OnActionByThread[NetworkThread.Main];
        
        // Lockers per thread (Main thread can be locked, others always process)
        private readonly Dictionary<NetworkThread, Locker> _lockers = new();
        
        // Action executor for applying actions to local game state
        private NetworkActionExecutor _executor;
        private TGameState _gameState;
        
        // Disposables for cleanup
        private readonly List<IDisposable> _disposables = new();
        
        public ActionProcessor(NetworkClient networkClient)
        {
            NetworkClient = networkClient;
            
            // Automatically initialize events and lockers for all registered NetworkThread values
            foreach (NetworkThread thread in NetworkThread.GetAll())
            {
                OnActionByThread[thread] = new Subject<INetworkAction>();
                
                // Only Main thread has a locker (can be locked during animations)
                // Other threads (PingPong, Chat, etc.) always process immediately
                _lockers[thread] = thread == NetworkThread.Main ? new Locker() : null;
            }
            
            // Process actions every frame for all threads
            _disposables.Add(Observable.IntervalFrame(1).Subscribe(_ => Update()));
        }
        
        /// <summary>
        /// Set the game state for action execution.
        /// Must be called before actions can be executed.
        /// </summary>
        public void SetGameState(TGameState gameState, DomainRegistry registry)
        {
            _gameState = gameState;
            _executor = new NetworkActionExecutor(registry);
        }
        
        /// <summary>
        /// Dispose of resources and subscriptions.
        /// </summary>
        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable?.Dispose();
            }
            _disposables.Clear();
        }
        
        /// <summary>
        /// Lock the Main thread processor temporarily.
        /// Returns a disposable that unlocks when disposed.
        /// </summary>
        public IDisposable Lock()
        {
            return Lock(NetworkThread.Main);
        }
        
        /// <summary>
        /// Lock a specific thread processor temporarily.
        /// Returns a disposable that unlocks when disposed.
        /// Note: Only Main thread has a locker by default. Other threads always process.
        /// </summary>
        public IDisposable Lock(NetworkThread thread)
        {
            var locker = _lockers[thread];
            if (locker == null)
                throw new InvalidOperationException($"Thread {thread} does not have a locker. Only Main thread can be locked by default.");
            
            return locker.Lock();
        }

        /// <summary>
        /// Check if a specific thread processor is currently locked.
        /// </summary>
        public bool IsThreadLocked(NetworkThread thread)
        {
            var locker = _lockers[thread];
            return locker != null && locker.IsLocked;
        }

        private void Update()
        {
            // Process each thread
            foreach (var thread in NetworkClient.ActionsByThread.Keys)
            {
                var queue = NetworkClient.ActionsByThread[thread];
                var locker = _lockers[thread];
                
                // Skip if no actions or thread is locked
                if (queue.Count <= 0)
                    continue;
                    
                if (locker != null && locker.IsLocked)
                    continue;
                
                // Dequeue the action
                var action = queue.Dequeue();
                
                // Check for ID desync before executing
                if (action.CurrentId > 0 && _gameState != null)
                {
                    var idProvider = _gameState.GetFirst<IdProviderDomain>();
                    if (idProvider != null && idProvider.CurrentCounter != action.CurrentId)
                    {
                        NetworkLogger.LogError($"[ActionProcessor] ID DESYNC DETECTED! Action {action.GetType().Name} sent with CurrentId={action.CurrentId}, but local counter is {idProvider.CurrentCounter}");
                    }
                }
                
                // Execute action on local game state
                _executor?.ExecuteAction(action, null, error => NetworkLogger.LogError($"[ActionProcessor] {error}"));
                
                // Emit the action to subscribers for UI updates
                OnActionByThread[thread].OnNext(action);
            }
        }
    }
}
