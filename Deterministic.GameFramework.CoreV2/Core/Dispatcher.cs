using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Deterministic.GameFramework.CoreV2.Logging;

namespace Deterministic.GameFramework.CoreV2;

// TODO: Currently it doesnt use counter, so it sometimes may disable when we dont want.
public class ActionRunnerDisposable(Dispatcher dispatcher, IEnumerable<IActionService>? servicesToDisable) : IDisposable
{
    private IEnumerable<IActionService>? _servicesToDisable = servicesToDisable;

    public void Dispose()
    {
        if (_servicesToDisable == null) return;
        
        dispatcher.DisableActions(_servicesToDisable);
        _servicesToDisable = null;
    }
}

// TODO: Currently it doesnt use counter, so it sometimes may disable when we dont want.
public class ReactionRunnerDisposable(Dispatcher dispatcher, IEnumerable<IReactionService>? servicesToDisable) : IDisposable
{
    private IEnumerable<IReactionService>? _servicesToDisable = servicesToDisable;

    public void Dispose()
    {
        if (_servicesToDisable == null) return;
        
        dispatcher.DisableReactions(_servicesToDisable);
        _servicesToDisable = null;
    }
}

public class Dispatcher
{
    private readonly Dictionary<int, object> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, GlobalState, Entity>> _byteActionRunners = new();
    // Changed from List to Dictionary to support UnregisterAction
    private readonly Dictionary<Type, Action<GlobalState>> _systemRunners = new();

    private readonly Func<Type, StableComponentId>? _serviceIdLookup;
    
    private System.Collections.BitArray _executionMask = new(256);
    private int _nextRuntimeId = 0;

    public Dispatcher(Func<Type, StableComponentId>? serviceIdLookup = null)
    {
        _serviceIdLookup = serviceIdLookup;
    }

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
            _systemRunners.Remove(service.ActionType);
            
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
            void Runner(TAction action, GlobalState state, Entity entity)
            {
                state.AddComponent(entity, action);
            }

            void ByteRunner(byte[] buffer, int offset, GlobalState state, Entity entity)
            {
                // Deserialize struct from raw bytes
                var span = new ReadOnlySpan<byte>(buffer, offset, Marshal.SizeOf<TAction>());
                var action = MemoryMarshal.Read<TAction>(span);
                state.AddComponent(entity, action);
            }

            _actionRunners[id] = (Action<TAction, GlobalState, Entity>)Runner;
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
        void SystemRunner(GlobalState state)
        {
            // Check if ActionService is enabled
            if (!_executionMask[actionService.RuntimeId]) return;

            state.ForEach((Entity entity, ref TAction action, ref TTarget target) =>
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
                var ctx = new Context(state, entity);

                if (RunPreReactions(ref action, ref target, ctx, preReactions))
                {
                    state.RemoveComponent<TAction>(entity);
                    return;
                }

                actionService.InternalExecute(action, ref target, ctx);
                RunPostReactions(ref action, ref target, ctx, postReactions);

                state.RemoveComponent<TAction>(entity);
            });
        }

        _systemRunners[typeof(TAction)] = SystemRunner;
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

    public void Update(GlobalState state)
    {
        foreach (var system in _systemRunners.Values)
        {
            system(state);
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

    public void Execute<TAction>(TAction action, GlobalState state, Entity entity) where TAction : struct, IAction
    {
        var id = ComponentId<TAction>.DenseId;
        if (_actionRunners.TryGetValue(id, out var runner))
        {
             ((Action<TAction, GlobalState, Entity>)runner)(action, state, entity);
        }
    }

    public void ExecuteByteAction(int localId, byte[] data, int offset, GlobalState state, Entity entity)
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
