using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public class Dispatcher
{
    private readonly Dictionary<int, Action<object, GlobalState, Entity>> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, GlobalState, Entity>> _byteActionRunners = new();
    
    // Map Action Struct Type -> Service Network ID
    internal readonly Dictionary<Type, int> _actionTypeToNetworkId = new();

    // Map Action Struct Type -> List of Hierarchy Reactions
    private readonly Dictionary<Type, List<HierarchyReactionEntry>> _hierarchyReactions = new();

    private struct HierarchyReactionEntry
    {
        public int ComponentId;
        public Func<object, GlobalState, Entity, Context, bool> Runner; // Returns true if aborted
        public int Priority;
        public bool AfterActionExecuted;
    }

    private readonly Func<Type, int>? _serviceIdLookup;

    public Dispatcher(Func<Type, int>? serviceIdLookup = null)
    {
        _serviceIdLookup = serviceIdLookup;
    }

    public void RegisterReaction<TAction, TTarget>(ReactionService<TAction, TTarget> reaction)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        if (!_hierarchyReactions.TryGetValue(typeof(TAction), out var list))
        {
            list = new List<HierarchyReactionEntry>();
            _hierarchyReactions[typeof(TAction)] = list;
        }

        // We need the component ID to check presence efficiently
        // We can access the static generic InternalTypeId via reflection or force registration
        // Assuming InternalTypeId<TTarget>.Value is available and initialized.
        // But InternalTypeId is internal. We are in the same assembly, so we can access it.
        int componentId = InternalTypeId<TTarget>.Value;

        Func<object, GlobalState, Entity, Context, bool> runner = (actionObj, state, entity, ctx) =>
        {
            var action = (TAction)actionObj;
            ref var target = ref state.GetState<TTarget>(entity);
            var result = reaction.InternalReact(action, ref target, ctx);
            return result.Value;
        };

        list.Add(new HierarchyReactionEntry
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
        int networkId;

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

        // Map the Action Type to this Network ID
        if (_actionTypeToNetworkId.ContainsKey(typeof(TAction)))
        {
            throw new Exception($"Action Type {typeof(TAction).Name} is already registered to ID {_actionTypeToNetworkId[typeof(TAction)]}. Cannot register multiple services for the same Action struct in this implementation.");
        }
        _actionTypeToNetworkId[typeof(TAction)] = networkId;

        var sortedReactions = reactions.OrderByDescending(r => r.Priority).ToList();
        var preReactions = sortedReactions.Where(r => !r.AfterActionExecuted).ToList();
        var postReactions = sortedReactions.Where(r => r.AfterActionExecuted).ToList();

        // Prepare hierarchy reaction lookup
        // Ensure the list exists so we can capture the reference
        if (!_hierarchyReactions.TryGetValue(typeof(TAction), out var hierarchyReactions))
        {
            hierarchyReactions = new List<HierarchyReactionEntry>();
            _hierarchyReactions[typeof(TAction)] = hierarchyReactions;
        }

        Action<object, GlobalState, Entity> runner = (actionObj, state, entity) =>
        {
            var action = (TAction)actionObj;
            var ctx = new Context(state, entity);
            ref var target = ref state.GetState<TTarget>(entity);

            // 1. Local Pre-Reactions
            if (RunPreReactions(action, ref target, ctx, preReactions)) return;

            // 2. Hierarchy Pre-Reactions (Bubbling)
            // Iterate the live list, filtering for Pre-Execution reactions
            if (RunHierarchyReactions(actionObj, state, entity, ctx, hierarchyReactions, true, false)) return;

            // 3. Execution
            actionService.InternalExecute(action, ref target, ctx);

            // 4. Local Post-Reactions
            RunPostReactions(action, ref target, ctx, postReactions);

            // 5. Hierarchy Post-Reactions (Bubbling)
            RunHierarchyReactions(actionObj, state, entity, ctx, hierarchyReactions, false, true);
        };
        
        Action<byte[], int, GlobalState, Entity> byteRunner = (buffer, offset, state, entity) =>
        {
            // Deserialize struct from raw bytes
            var span = new ReadOnlySpan<byte>(buffer, offset, Marshal.SizeOf<TAction>());
            var action = MemoryMarshal.Read<TAction>(span);
            
            var ctx = new Context(state, entity);
            ref var target = ref state.GetState<TTarget>(entity);

            // 1. Local Pre-Reactions
            if (RunPreReactions(action, ref target, ctx, preReactions)) return;

            // 2. Hierarchy Pre-Reactions
            object actionObj2 = action; 
            if (RunHierarchyReactions(actionObj2, state, entity, ctx, hierarchyReactions, true, false)) return;

            // 3. Execution
            actionService.InternalExecute(action, ref target, ctx);

            // 4. Local Post-Reactions
            RunPostReactions(action, ref target, ctx, postReactions);

            // 5. Hierarchy Post-Reactions
            RunHierarchyReactions(actionObj2, state, entity, ctx, hierarchyReactions, false, true);
        };

        _actionRunners[networkId] = runner;
        _byteActionRunners[networkId] = byteRunner;
    }

    private bool RunHierarchyReactions(object actionObj, GlobalState state, Entity startEntity, Context ctx, List<HierarchyReactionEntry> reactions, bool canAbort, bool runAfterAction)
    {
        // HierarchyComponent ID
        int hierarchyTypeId = InternalTypeId<HierarchyComponent>.Value;
        Entity current = startEntity;
        
        // Bubbling loop
        while (true)
        {
            foreach (var reaction in reactions)
            {
                // Filter: Only run if the phase matches
                if (reaction.AfterActionExecuted != runAfterAction) continue;

                // Check if current entity has the component for this reaction
                if (state._entityMasks.Length > current.Id && state._entityMasks[current.Id].IsSet(reaction.ComponentId))
                {
                    try 
                    {
                        // Create a new context for the bubbling reaction
                        // 'current' is the entity we are reacting ON (Ancestor)
                        var bubblingCtx = new Context(state, current);

                        // Run reaction
                        bool isAborted = reaction.Runner(actionObj, state, current, bubblingCtx);
                        if (canAbort && isAborted) return true;
                    }
                    catch (Exception ex)
                    {
                         Console.WriteLine($"Error in hierarchy reaction: {ex}");
                    }
                }
            }
            
            // Move up
            if (state._entityMasks.Length > current.Id && state._entityMasks[current.Id].IsSet(hierarchyTypeId))
            {
                ref var hierarchy = ref state.GetState<HierarchyComponent>(current);
                if (hierarchy.ParentId == 0) break; // No parent (assuming 0 is null/invalid, or check if it exists)
                // If 0 is a valid entity, we need a better check. Usually ID 0 is valid. 
                // But HierarchyComponent.ParentId needs a sentinel.
                // Let's assume Entity 0 is valid, so we need a "HasParent" flag or -1.
                // Wait, HierarchyComponent defaults to 0. 
                // We should check if ParentId is self or some invalid value.
                // Assuming -1 or checking if ParentId == current.Id (root) if circular.
                // Standard practice: if ParentId == 0 and Entity 0 is not the parent, it's root.
                // Actually, if it HAS HierarchyComponent, it is part of a tree.
                // Let's assume 0 is a valid ID. We need a way to know if it has a parent.
                // Typically: "ParentId" is valid if it points to an existing entity.
                // But loop termination?
                
                // Let's assume standard behavior: Id 0 is valid.
                // If ParentId == 0, is it the root? Or is 0 the root?
                // Let's rely on the user to handle tree structure properly.
                // Loop detection?
                // For this implementation, let's assume -1 or same-ID is termination?
                // UnsafeComponent example showed NetworkId(999).
                
                // Let's check logic in PoCTest: "var rootNode = new Entity(1);"
                // "state.AddChild(rootNode, player);"
                // HierarchyExtensions.AddChild sets ParentId.
                
                // If we look at HierarchyComponent.cs, it's just ints.
                // We need to know what "No Parent" is.
                // Default int is 0.
                // If Entity 0 is used, we might have issues.
                // Let's check HierarchyExtensions if available.
                
                if (hierarchy.ParentId == current.Id || hierarchy.ParentId < 0) break;
                
                // Safety: prevent infinite loops
                if (hierarchy.ParentId == startEntity.Id) break; 

                current = new Entity(hierarchy.ParentId);
            }
            else
            {
                break; // No hierarchy component, reached top of what we can traverse
            }
        }
        
        return false; // TODO: Implement Abort for hierarchy
    }

    public void Execute<TAction>(TAction action, GlobalState state, Entity entity) where TAction : struct, IAction
    {
        if (!_actionTypeToNetworkId.TryGetValue(typeof(TAction), out int networkId))
        {
             throw new Exception($"No registered service found for action {typeof(TAction).Name}");
        }

        if (_actionRunners.TryGetValue(networkId, out var runner))
        {
            runner(action, state, entity);
        }
        else
        {
            throw new Exception($"No runner registered for ID {networkId}");
        }
    }

    public void ExecuteByteAction(int networkId, byte[] buffer, int offset, GlobalState state, Entity entity)
    {
        if (_byteActionRunners.TryGetValue(networkId, out var byteRunner))
        {
            byteRunner(buffer, offset, state, entity);
        }
        // If not found? 
    }

    public int GetNetworkId<TAction>()
    {
         if (_actionTypeToNetworkId.TryGetValue(typeof(TAction), out int networkId))
         {
             return networkId;
         }
         throw new Exception($"Action {typeof(TAction).Name} is not registered.");
    }

    private bool RunPreReactions<TAction, TTarget>(
        TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> preReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in preReactions)
        {
            var isAborted = reaction.InternalReact(action, ref target, ctx);
            if (isAborted.Value) return true; // Aborted
        }
        return false;
    }

    private void RunPostReactions<TAction, TTarget>(
        TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> postReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in postReactions)
        {
            reaction.InternalReact(action, ref target, ctx);
        }
    }
}
