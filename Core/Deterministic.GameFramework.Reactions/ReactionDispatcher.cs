using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.DAR;

public class ReactionDispatcher : Dispatcher
{
    public override void RegisterServices(IEnumerable<IActionService> actionServices)
    {
        RegisterServices(actionServices, Enumerable.Empty<IReactionService>());
    }

    public void RegisterServices(IEnumerable<IActionService> actionServices, IEnumerable<IReactionService> reactionServices)
    {
        var reactionMap = reactionServices
            .GroupBy(r => (r.ActionType, r.TargetType))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var service in actionServices)
        {
            var actionType = service.ActionType;
            var targetType = service.TargetType;

            try
            {
                if (IsActionRegistered(actionType)) continue;

                var key = (actionType, targetType);
                object[] reactions;

                if (reactionMap.TryGetValue(key, out var reactionList))
                {
                    var reactionType = typeof(ReactionService<,>).MakeGenericType(actionType, targetType);
                    var reactionArray = Array.CreateInstance(reactionType, reactionList.Count);
                    for (int i = 0; i < reactionList.Count; i++)
                    {
                        reactionArray.SetValue(reactionList[i], i);
                    }
                    reactions = (object[])(object)reactionArray;
                }
                else
                {
                    var reactionType = typeof(ReactionService<,>).MakeGenericType(actionType, targetType);
                    reactions = (object[])(object)Array.CreateInstance(reactionType, 0);
                }

                var registerMethod = typeof(ReactionDispatcher).GetMethod(nameof(RegisterActionWithReactions))?
                    .MakeGenericMethod(actionType, targetType);

                registerMethod?.Invoke(this, new object[] { service, reactions });
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[ReactionDispatcher] Failed to register {service.GetType().Name}: {ex.Message}");
            }
        }
    }

    public override void UnregisterServices(IEnumerable<IActionService> actionServices)
    {
        UnregisterServices(actionServices, Enumerable.Empty<IReactionService>());
    }

    public void UnregisterServices(IEnumerable<IActionService> actionServices, IEnumerable<IReactionService> reactionServices)
    {
        foreach (var service in actionServices)
        {
            if (_systemRunners.TryGetValue(service.ActionType, out var runner))
            {
                _systemRunners.Remove(service.ActionType);
                _orderedSystems.Remove(runner);
            }

            DisableAction(service);
        }

        foreach (var service in reactionServices)
        {
            DisableReaction(service);
        }
    }

    public void RegisterActionWithReactions<TAction, TTarget>(
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

        EnsureActionRunnersRegistered<TAction>();

        var reactionServices = reactions.ToList();

        foreach (var r in reactionServices)
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

        ComponentActionEntity2<TAction, TTarget, ReactionSystemContext<TAction, TTarget>> forEachDelegate = (ReactionSystemContext<TAction, TTarget> ctxState, Entity entity, ref TAction action, ref TTarget target) =>
        {
            if (ctxState.Dispatcher.ActionDispatcher == null) throw new InvalidOperationException("ActionDispatcher must be set before running systems.");

            var ctx = new Context(ctxState.World, entity, ctxState.Dispatcher.ActionDispatcher);

            long savedPid = ctxState.World.CurrentActionPredictionId;
            int pendingPredDense = Dispatcher.GetActionPendingPredictionDenseId();
            bool hasPendingPrediction = pendingPredDense >= 0
                && entity.Id < ctxState.World.EntityMasks.Length
                && ctxState.World.EntityMasks[entity.Id].IsSet(pendingPredDense);
            if (hasPendingPrediction)
            {
                ctxState.World.CurrentActionPredictionId = ctxState.World.GetComponent<ActionPendingPrediction>(entity).PredictionId;
            }

            try
            {
                if (RunPreReactions(ref action, ref target, ctx, ctxState.PreReactions))
                {
                    ctxState.World.RemoveComponent<TAction>(entity);
                    if (hasPendingPrediction) ctxState.World.RemoveComponent<ActionPendingPrediction>(entity);
                    return;
                }

                ctxState.Service.InternalExecute(action, ref target, ctx);
                RunPostReactions(ref action, ref target, ctx, ctxState.PostReactions);

                ctxState.World.RemoveComponent<TAction>(entity);
                if (hasPendingPrediction) ctxState.World.RemoveComponent<ActionPendingPrediction>(entity);
            }
            finally
            {
                ctxState.World.CurrentActionPredictionId = savedPid;
            }
        };

        var reusableContext = new ReactionSystemContext<TAction, TTarget>
        {
            Dispatcher = this,
            Service = actionService,
            PreReactions = preReactions,
            PostReactions = postReactions
        };

        void SystemRunner(EntityWorld state)
        {
            if (!_executionMask[actionService.RuntimeId]) return;

            reusableContext.World = state;
            state.ForEach(reusableContext, forEachDelegate);
        }

        _systemRunners[typeof(TAction)] = SystemRunner;
        _orderedSystems.Add(SystemRunner);
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

    public bool IsReactionEnabled(IReactionService reaction)
    {
        return reaction.RuntimeId != -1 &&
               reaction.RuntimeId < _executionMask.Length &&
               _executionMask[reaction.RuntimeId];
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
            if (isAborted.Value) return true;
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

    private class ReactionSystemContext<TAction, TTarget>
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        public EntityWorld World;
        public ReactionDispatcher Dispatcher;
        public ActionService<TAction, TTarget> Service;
        public List<ReactionService<TAction, TTarget>> PreReactions;
        public List<ReactionService<TAction, TTarget>> PostReactions;
    }
}
