using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Deterministic.GameFramework.CoreV2;

public class Dispatcher
{
    private readonly Dictionary<int, Action<object, GlobalState, Entity>> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, GlobalState, Entity>> _byteActionRunners = new();
    
    // Map Action Struct Type -> Service Dense ID
    internal readonly Dictionary<Type, int> _actionTypeToDenseId = new();
    internal readonly Dictionary<int, Type> _denseIdToType = new();

    // Map Action Struct Type -> List of Additional Local Reactions (different TTarget)
    // Stored as object, cast to List<AdditionalReactionEntry<TAction>> at runtime
    private readonly Dictionary<Type, object> _additionalReactions = new();

    private class AdditionalReactionEntry<TAction>
    {
        public int ComponentId;
        public required ReactionRunner<TAction> Runner; 
        public int Priority;
        public bool AfterActionExecuted;
    }

    private delegate bool ReactionRunner<TAction>(ref TAction action, GlobalState state, Entity entity, Context ctx);

    private readonly Func<Type, Guid>? _serviceIdLookup;

    public Dispatcher(Func<Type, Guid>? serviceIdLookup = null)
    {
        _serviceIdLookup = serviceIdLookup;
    }

    public void RegisterReaction<TAction, TTarget>(ReactionService<TAction, TTarget> reaction)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        if (!_additionalReactions.TryGetValue(typeof(TAction), out var listObj))
        {
            listObj = new List<AdditionalReactionEntry<TAction>>();
            _additionalReactions[typeof(TAction)] = listObj;
        }
        
        var list = (List<AdditionalReactionEntry<TAction>>)listObj;
        // Console.WriteLine($"[Dispatcher] Registering Reaction for {typeof(TAction).Name}. Component: {typeof(TTarget).Name}. List Hash: {list.GetHashCode()}");

        int componentId = InternalTypeId<TTarget>.Value;

        ReactionRunner<TAction> runner = (ref TAction action, GlobalState state, Entity entity, Context ctx) =>
        {
            ref var target = ref state.GetComponent<TTarget>(entity);
            var result = reaction.InternalReact(ref action, ref target, ctx);
            return result.Value;
        };

        list.Add(new AdditionalReactionEntry<TAction>
        {
            ComponentId = componentId,
            Runner = runner,
            Priority = reaction.Priority,
            AfterActionExecuted = reaction.AfterActionExecuted
        });

        // Re-sort list by priority
        list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public void RegisterAction<TAction, TTarget>(
        ActionService<TAction, TTarget> actionService, 
        IEnumerable<ReactionService<TAction, TTarget>> reactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        // ... (Registration logic remains same) ...
        // Resolve NetworkId from the Service class attribute
        var serviceType = actionService.GetType();
        Guid networkId;

        if (_serviceIdLookup != null)
        {
            try 
            {
                networkId = _serviceIdLookup(serviceType);
            }
            catch (KeyNotFoundException)
            {
                 throw new Exception($"ActionService {serviceType.Name} not found in provided NetworkId registry.");
            }
        }
        else
        {
            var networkIdAttr = serviceType.GetCustomAttribute<NetworkIdAttribute>();
            if (networkIdAttr == null)
            {
                throw new Exception($"ActionService {serviceType.Name} is missing [NetworkId] attribute.");
            }
            networkId = networkIdAttr.Id;
        }

        // Resolve to DenseId for runtime efficiency
        int denseId = ComponentTypeRegistry.GetOrRegister(networkId, typeof(TAction));

        // Map the Action Type to this Dense ID
        if (_actionTypeToDenseId.ContainsKey(typeof(TAction)))
        {
            throw new Exception($"Action Type {typeof(TAction).Name} is already registered to ID {_actionTypeToDenseId[typeof(TAction)]}. Cannot register multiple services for the same Action struct in this implementation.");
        }
        _actionTypeToDenseId[typeof(TAction)] = denseId;
        _denseIdToType[denseId] = typeof(TAction);

        var sortedReactions = reactions.OrderByDescending(r => r.Priority).ToList();
        var preReactions = sortedReactions.Where(r => !r.AfterActionExecuted).ToList();
        var postReactions = sortedReactions.Where(r => r.AfterActionExecuted).ToList();

        // Prepare additional reaction lookup
        // FIX: Ensure the list exists so we capture a valid reference even if reactions are registered later.
        if (!_additionalReactions.TryGetValue(typeof(TAction), out var listObj))
        {
            listObj = new List<AdditionalReactionEntry<TAction>>();
            _additionalReactions[typeof(TAction)] = listObj;
        }
        var additionalReactions = (List<AdditionalReactionEntry<TAction>>)listObj;
        // Console.WriteLine($"[Dispatcher] RegisterAction for {typeof(TAction).Name}. AdditionalReactions List Hash: {additionalReactions.GetHashCode()}");

        Action<object, GlobalState, Entity> runner = (actionObj, state, entity) =>
        {
            var action = (TAction)actionObj;
            // Console.WriteLine($"[Dispatcher] Executing {typeof(TAction).Name}: {JsonSerializer.Serialize(action, new JsonSerializerOptions { IncludeFields = true })}");
            var ctx = new Context(state, entity);
            ref var target = ref state.GetComponent<TTarget>(entity);

            // 1. Local Pre-Reactions (Standard)
            if (RunPreReactions(ref action, ref target, ctx, preReactions)) return;

            // 2. Additional Pre-Reactions (Different Components, Local)
            if (RunAdditionalReactions(ref action, state, entity, ctx, additionalReactions, true, false)) return;

            // 3. Execution
            actionService.InternalExecute(action, ref target, ctx);

            // 4. Local Post-Reactions (Standard)
            RunPostReactions(ref action, ref target, ctx, postReactions);

            // 5. Additional Post-Reactions (Different Components, Local)
            RunAdditionalReactions(ref action, state, entity, ctx, additionalReactions, false, true);
        };
        
        Action<byte[], int, GlobalState, Entity> byteRunner = (buffer, offset, state, entity) =>
        {
            // Deserialize struct from raw bytes
            var span = new ReadOnlySpan<byte>(buffer, offset, Marshal.SizeOf<TAction>());
            var action = MemoryMarshal.Read<TAction>(span);
            
            // Console.WriteLine($"[Dispatcher] Executing (Byte) {typeof(TAction).Name}: {JsonSerializer.Serialize(action, new JsonSerializerOptions { IncludeFields = true })}");
            var ctx = new Context(state, entity);
            ref var target = ref state.GetComponent<TTarget>(entity);

            // 1. Local Pre-Reactions
            if (RunPreReactions(ref action, ref target, ctx, preReactions)) return;

            // 2. Additional Pre-Reactions
            if (RunAdditionalReactions(ref action, state, entity, ctx, additionalReactions, true, false)) return;

            // 3. Execution
            actionService.InternalExecute(action, ref target, ctx);

            // 4. Local Post-Reactions
            RunPostReactions(ref action, ref target, ctx, postReactions);

            // 5. Additional Post-Reactions
            RunAdditionalReactions(ref action, state, entity, ctx, additionalReactions, false, true);
        };

        _actionRunners[denseId] = runner;
        _byteActionRunners[denseId] = byteRunner;
    }

    private bool RunAdditionalReactions<TAction>(ref TAction action, GlobalState state, Entity entity, Context ctx, List<AdditionalReactionEntry<TAction>> reactions, bool canAbort, bool runAfterAction)
    {
        // Console.WriteLine($"[Dispatcher] RunAdditionalReactions for {typeof(TAction).Name}:{JsonSerializer.Serialize(action)}. Count: {reactions.Count}, Entity: {entity.Id}");
        foreach (var reaction in reactions)
        {
            // Filter: Only run if the phase matches
            if (reaction.AfterActionExecuted != runAfterAction) continue;

            // Check if current entity has the component for this reaction
            bool hasComponent = state._entityMasks.Length > entity.Id && state._entityMasks[entity.Id].IsSet(reaction.ComponentId);
            // Console.WriteLine($"[Dispatcher] Checking reaction for ComponentId {reaction.ComponentId}. HasComponent: {hasComponent}");
            
            if (hasComponent)
            {
                try 
                {
                    // Run reaction
                    bool isAborted = reaction.Runner(ref action, state, entity, ctx);
                    if (canAbort && isAborted) return true;
                }
                catch (Exception ex)
                {
                        Console.WriteLine($"Error in additional reaction: {ex}");
                }
            }
        }
        return false;
    }

    public Type? GetActionType(int denseId)
    {
        return _byteActionRunners.ContainsKey(denseId) && _denseIdToType.TryGetValue(denseId, out var type) 
            ? type 
            : null;
    }

    public void Execute<TAction>(TAction action, GlobalState state, Entity entity) where TAction : struct, IAction
    {
        if (!_actionTypeToDenseId.TryGetValue(typeof(TAction), out int denseId))
        {
             throw new Exception($"No registered service found for action {typeof(TAction).Name}");
        }

        if (_actionRunners.TryGetValue(denseId, out var runner))
        {
            runner(action, state, entity);
        }
        else
        {
            throw new Exception($"No runner registered for ID {denseId}");
        }
    }

    public void ExecuteByteAction(int denseId, byte[] buffer, int offset, GlobalState state, Entity entity)
    {
        if (_byteActionRunners.TryGetValue(denseId, out var byteRunner))
        {
            byteRunner(buffer, offset, state, entity);
        }
        // If not found? 
    }

    public int GetDenseId<TAction>()
    {
         if (_actionTypeToDenseId.TryGetValue(typeof(TAction), out int denseId))
         {
             return denseId;
         }
         throw new Exception($"Action {typeof(TAction).Name} is not registered.");
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
            reaction.InternalReact(ref action, ref target, ctx);
        }
    }
}
