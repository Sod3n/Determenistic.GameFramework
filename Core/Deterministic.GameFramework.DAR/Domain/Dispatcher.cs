using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.DAR;

/// <summary>
/// Manages the LOGICAL execution and wiring of actions.
/// Responsible for:
/// 1. Registry: Maps Action Types -> ActionService (Logic) and ReactionServices.
/// 2. Execution: Defines HOW an action is processed (Pre-reactions -> Action -> Post-reactions).
/// 3. System Generation: Creates ECS systems that iterate over entities with Action components.
/// 
/// Does NOT care about time. It processes whatever components are currently in the EntityWorld.
/// </summary>
public class Dispatcher
{
    private readonly Dictionary<int, object> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, EntityWorld, Entity>> _byteActionRunners = new();
    // Changed from List to Dictionary to support UnregisterAction
    private readonly Dictionary<Type, Action<EntityWorld>> _systemRunners = new();
    // Maintain a list for zero-allocation iteration
    private readonly List<Action<EntityWorld>> _orderedSystems = new();

    private System.Collections.BitArray _executionMask = new(256);
    private int _nextRuntimeId = 0;
    
    public IActionDispatcher ActionDispatcher { get; set; }
    
    
    public void RegisterServices(IEnumerable<IActionService> actionServices, IEnumerable<IReactionService> reactionServices)
    {
        // 1. Group Reactions by (ActionType, TargetType)
        var reactionMap = reactionServices
            .GroupBy(r => (r.ActionType, r.TargetType))
            .ToDictionary(g => g.Key, g => g.ToList());

        // 2. Register ActionServices
        var consumedReactions = new HashSet<(Type ActionType, Type TargetType)>();

        foreach (var service in actionServices)
        {
            var actionType = service.ActionType;
            var targetType = service.TargetType;

            try
            {
                if (IsActionRegistered(actionType)) continue;

                // Find matching reactions
                var key = (actionType, targetType);
                object[] reactions;

                if (reactionMap.TryGetValue(key, out var reactionList))
                {
                    // Create array of specific reaction type
                    var reactionType = typeof(ReactionService<,>).MakeGenericType(actionType, targetType);
                    var reactionArray = Array.CreateInstance(reactionType, reactionList.Count);
                    for (int i = 0; i < reactionList.Count; i++)
                    {
                        reactionArray.SetValue(reactionList[i], i);
                    }
                    reactions = (object[])(object)reactionArray; // Double cast trick or just use Array
                    consumedReactions.Add(key);
                }
                else
                {
                    var reactionType = typeof(ReactionService<,>).MakeGenericType(actionType, targetType);
                    reactions = (object[])(object)Array.CreateInstance(reactionType, 0);
                }

                // Call RegisterAction
                var registerMethod = GetType().GetMethod(nameof(RegisterAction))?
                    .MakeGenericMethod(actionType, targetType);

                registerMethod?.Invoke(this, new object[] { service, reactions });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Dispatcher] Failed to register {service.GetType().Name}: {ex.Message}");
            }
        }
    }

    public void UnregisterServices(IEnumerable<IActionService> actionServices, IEnumerable<IReactionService> reactionServices)
    {
        foreach (var service in actionServices)
        {
            // Remove from System Runners (stops processing and makes IsActionRegistered return false)
            if (_systemRunners.TryGetValue(service.ActionType, out var runner))
            {
                _systemRunners.Remove(service.ActionType);
                _orderedSystems.Remove(runner);
            }
            
            // Clear Execution Mask
            DisableAction(service);
        }
        
        foreach (var service in reactionServices)
        {
            DisableReaction(service);
        }
    }

    private void EnsureMaskCapacity(int id)
    {
        if (id >= _executionMask.Length)
        {
            var newLength = Math.Max(id + 1, _executionMask.Length * 2);
            _executionMask.Length = newLength;
        }
    }

    public bool IsActionRegistered(Type actionType)
    {
        return _systemRunners.ContainsKey(actionType);
    }
    
    public void EnableReaction(IReactionService reaction)
    {
        if (reaction.RuntimeId != -1)
        {
            EnsureMaskCapacity(reaction.RuntimeId);
            _executionMask[reaction.RuntimeId] = true;
        }
    }

    public void DisableReaction(IReactionService reaction)
    {
        if (reaction.RuntimeId != -1 && reaction.RuntimeId < _executionMask.Length)
        {
            _executionMask[reaction.RuntimeId] = false;
        }
    }

    public void RegisterAction<TAction, TTarget>(
        ActionService<TAction, TTarget> actionService, 
        IEnumerable<ReactionService<TAction, TTarget>> reactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        if (actionService.RuntimeId == -1)
        {
            actionService.RuntimeId = _nextRuntimeId++;
            EnsureMaskCapacity(actionService.RuntimeId);
        }
        _executionMask[actionService.RuntimeId] = true;
        
        var id = ComponentId<TAction>.DenseId;

        // Map the Action Type to this Dense ID
        if (!_actionRunners.ContainsKey(id))
        {
            // 2. Setup Dispatch Runners (Just AddComponent)
            void Runner(TAction action, EntityWorld state, Entity entity)
            {
                state.AddComponent(entity, action);
            }

            void ByteRunner(byte[] buffer, int offset, EntityWorld state, Entity entity)
            {
                // Deserialize struct from raw bytes
                var span = new ReadOnlySpan<byte>(buffer, offset, System.Runtime.CompilerServices.Unsafe.SizeOf<TAction>());
                var action = MemoryMarshal.Read<TAction>(span);
                state.AddComponent(entity, action);
            }

            _actionRunners[id] = (Action<TAction, EntityWorld, Entity>)Runner;
            _byteActionRunners[id] = ByteRunner;
        }
        
        // Also register reactions runtime IDs
        var reactionServices = reactions.ToList();
        
        foreach(var r in reactionServices)
        {
            if (r.RuntimeId == -1)
            {
                r.RuntimeId = _nextRuntimeId++;
                EnsureMaskCapacity(r.RuntimeId);
            }
            _executionMask[r.RuntimeId] = true;
        }

        var sortedReactions = reactionServices.OrderByDescending(r => r.Priority).ToList();
        var preReactions = sortedReactions.Where(r => !r.AfterActionExecuted).ToList();
        var postReactions = sortedReactions.Where(r => r.AfterActionExecuted).ToList();

        // 1. Create System Runner (ECS Loop)
        // Cache the delegate to avoid allocation every tick
        ComponentActionEntity2<TAction, TTarget, SystemContext<TAction, TTarget>> forEachDelegate = (SystemContext<TAction, TTarget> ctxState, Entity entity, ref TAction action, ref TTarget target) =>
        {
#if DEBUG
            try
            {
                var json = JsonSerializer.Serialize(action, new JsonSerializerOptions { IncludeFields = true });
                ILogger.Log($"[ActionSystem] Processing {typeof(TAction).Name} on Entity {entity.Id}: {json}");
            }
            catch
            {
                ILogger.Log($"[ActionSystem] Processing {typeof(TAction).Name} on Entity {entity.Id}: <serialization failed>");
            }
#endif
            if (ctxState.Dispatcher.ActionDispatcher == null) throw new InvalidOperationException("ActionDispatcher must be set before running systems.");
            
            var ctx = new Context(ctxState.World, entity, ctxState.Dispatcher.ActionDispatcher);

            if (ctxState.Dispatcher.RunPreReactions(ref action, ref target, ctx, ctxState.PreReactions))
            {
                ctxState.World.RemoveComponent<TAction>(entity);
                return;
            }

            ctxState.Service.InternalExecute(action, ref target, ctx);
            ctxState.Dispatcher.RunPostReactions(ref action, ref target, ctx, ctxState.PostReactions);

            ctxState.World.RemoveComponent<TAction>(entity);
        };

        // Reuse context object to avoid struct copy/potential boxing issues
        var reusableContext = new SystemContext<TAction, TTarget>
        {
            Dispatcher = this,
            Service = actionService,
            PreReactions = preReactions,
            PostReactions = postReactions
        };

        void SystemRunner(EntityWorld state)
        {
            // Check if ActionService is enabled
            if (!_executionMask[actionService.RuntimeId]) return;

            reusableContext.World = state;
            state.ForEach(reusableContext, forEachDelegate);
        }

        _systemRunners[typeof(TAction)] = SystemRunner;
        _orderedSystems.Add(SystemRunner);
    }

    private class SystemContext<TAction, TTarget>
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        public EntityWorld World;
        public Dispatcher Dispatcher;
        public ActionService<TAction, TTarget> Service;
        public List<ReactionService<TAction, TTarget>> PreReactions;
        public List<ReactionService<TAction, TTarget>> PostReactions;
    }

    public bool IsActionEnabled(IActionService actionService)
    {
        return actionService.RuntimeId != -1 && 
               actionService.RuntimeId < _executionMask.Length && 
               _executionMask[actionService.RuntimeId];
    }

    public bool IsReactionEnabled(IReactionService reaction)
    {
        return reaction.RuntimeId != -1 && 
               reaction.RuntimeId < _executionMask.Length && 
               _executionMask[reaction.RuntimeId];
    }

    public ActionRunnerDisposable EnableActions(IEnumerable<IActionService> actionServices)
    {
        var servicesToEnable = new List<IActionService>();
        foreach (var service in actionServices)
        {
            if (!IsActionEnabled(service))
            {
                EnableAction(service);
                servicesToEnable.Add(service);
            }
        }
        return new ActionRunnerDisposable(this, servicesToEnable);
    }

    public void DisableActions(IEnumerable<IActionService> actionServices)
    {
        foreach (var service in actionServices)
        {
            DisableAction(service);
        }
    }

    public ReactionRunnerDisposable EnableReactions(IEnumerable<IReactionService> reactionServices)
    {
        var servicesToEnable = new List<IReactionService>();
        foreach (var service in reactionServices)
        {
            if (!IsReactionEnabled(service))
            {
                EnableReaction(service);
                servicesToEnable.Add(service);
            }
        }
        return new ReactionRunnerDisposable(this, servicesToEnable);
    }

    public void DisableReactions(IEnumerable<IReactionService> reactionServices)
    {
        foreach (var service in reactionServices)
        {
            DisableReaction(service);
        }
    }

    public void EnableAction(IActionService actionService)
    {
        if (actionService.RuntimeId != -1)
        {
            EnsureMaskCapacity(actionService.RuntimeId);
            _executionMask[actionService.RuntimeId] = true;
        }
    }

    public void DisableAction(IActionService actionService)
    {
        if (actionService.RuntimeId != -1 && actionService.RuntimeId < _executionMask.Length)
        {
            _executionMask[actionService.RuntimeId] = false;
        }
    }

    public void Update(EntityWorld state)
    {
        var count = _orderedSystems.Count;
        for (int i = 0; i < count; i++)
        {
            _orderedSystems[i](state);
        }
    }

    public int GetDenseId<TAction>() where TAction : struct, IAction
    {
        if (!IsActionRegistered(typeof(TAction)))
        {
            throw new Exception($"Action {typeof(TAction).Name} is not registered in Dispatcher.");
        }
        return ComponentId<TAction>.DenseId;
    }
    
    public Type? GetActionType(int localId)
    {
        return _byteActionRunners.ContainsKey(localId) && ComponentId.TryGetType(new DenseComponentId(localId), out var type) 
            ? type 
            : null;
    }

    public void Execute<TAction>(TAction action, EntityWorld state, Entity entity) where TAction : struct, IAction
    {
        var id = ComponentId<TAction>.DenseId;
        if (_actionRunners.TryGetValue(id, out var runner))
        {
             ((Action<TAction, EntityWorld, Entity>)runner)(action, state, entity);
        }
    }

    public void ExecuteByteAction(int localId, byte[] data, int offset, EntityWorld state, Entity entity)
    {
        if (_byteActionRunners.TryGetValue(localId, out var runner))
        {
            runner(data, offset, state, entity);
        }
    }

    private bool RunPreReactions<TAction, TTarget>(
        ref TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> preReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in preReactions)
        {
            if (!_executionMask[reaction.RuntimeId]) continue;
            var isAborted = reaction.InternalReact(ref action, ref target, ctx);
            if (isAborted.Value) return true; // Aborted
        }
        return false;
    }

    private void RunPostReactions<TAction, TTarget>(
        ref TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> postReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in postReactions)
        {
            if (!_executionMask[reaction.RuntimeId]) continue;
            reaction.InternalReact(ref action, ref target, ctx);
        }
    }
}
