using System;
using System.Collections.Generic;
using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Reusable helper for deserializing and executing network actions.
/// Used by both server (GameHub) and client (ActionProcessor).
/// </summary>
public class NetworkActionExecutor
{
    private readonly DomainRegistry _registry;
    
    public event Action<INetworkAction> BeforeAction;
    
    /// <summary>
    /// Optional validation hook that wraps action execution.
    /// If set, the validator can execute the action with logging and comparison.
    /// Signature: (action, executePrimary) => bool (returns true if validation passed)
    /// </summary>
    public Func<INetworkAction, Action, bool>? ValidationHook { get; set; }
    
    public NetworkActionExecutor(DomainRegistry registry)
    {
        _registry = registry;
    }
    
    /// <summary>
    /// Execute a network action on the target domain.
    /// </summary>
    /// <param name="action">The action to execute</param>
    /// <param name="executorId">Optional executor ID to override (for server-side security)</param>
    /// <param name="onError">Error callback</param>
    public bool ExecuteAction(INetworkAction action, Guid? executorId = null, System.Action<string>? onError = null)
    {
        try
        {
            // Get target domain (use DomainId if set, otherwise use root GameState)
            LeafDomain domain;
            if (action.DomainId == 0)
            {
                // DomainId 0 means target the root GameState
                domain = _registry.GetDomain(0);
            }
            else
            {
                domain = _registry.GetDomain(action.DomainId);
            }
            
            if (domain == null)
            {
                onError?.Invoke($"Domain {action.DomainId} not found");
                return false;
            }

            // Override ExecutorId if provided (server-side security)
            if (executorId.HasValue)
            {
                action.ExecutorId = executorId.Value;
            }

            // Execute with validation if hook is set
            if (ValidationHook != null)
            {
                bool isValid = ValidationHook(action, () =>
                {
                    BeforeAction?.Invoke(action);
                    action.Execute(domain);
                });
                return isValid;
            }
            else
            {
                // Normal execution without validation
                BeforeAction?.Invoke(action);
                action.Execute(domain);
                return true;
            }
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Error executing action {action.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deserialize and execute a batch of actions.
    /// </summary>
    /// <param name="actionsJson">JSON string containing the actions</param>
    /// <param name="executorId">Optional executor ID to override on all actions (for server-side security)</param>
    /// <param name="onError">Error callback</param>
    public int ExecuteBatch(string actionsJson, Guid? executorId = null, System.Action<string>? onError = null)
    {
        try
        {
            // Deserialize batch directly to actions
            var actions = JsonSerializer.FromJson<List<INetworkAction>>(actionsJson);
            if (actions == null || actions.Count == 0)
            {
                return 0;
            }

            int successCount = 0;
            foreach (var action in actions)
            {
                if (ExecuteAction(action, executorId, onError))
                {
                    successCount++;
                }
            }

            return successCount;
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Error executing batch: {ex.Message}");
            return 0;
        }
    }
}
