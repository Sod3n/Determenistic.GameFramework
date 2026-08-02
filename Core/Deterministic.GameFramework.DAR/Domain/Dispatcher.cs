using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.DAR;

public class Dispatcher
{
    private readonly Dictionary<int, object> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, EntityWorld, Entity>> _byteActionRunners = new();
    private readonly HashSet<int> _registeredActionDenseIds = new();
    protected readonly Dictionary<Type, Action<EntityWorld>> _systemRunners = new();
    protected readonly List<Action<EntityWorld>> _orderedSystems = new();

    protected System.Collections.BitArray _executionMask = new(256);
    protected int _nextRuntimeId = 0;

    public IActionDispatcher ActionDispatcher { get; set; }


    public virtual void RegisterServices(IEnumerable<IActionService> actionServices)
    {
        foreach (var service in actionServices)
        {
            var actionType = service.ActionType;
            var targetType = service.TargetType;

            try
            {
                if (IsActionRegistered(actionType)) continue;

                var registerMethod = GetType().GetMethod(nameof(RegisterAction))?
                    .MakeGenericMethod(actionType, targetType);

                registerMethod?.Invoke(this, new object[] { service });
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[Dispatcher] Failed to register {service.GetType().Name}: {ex.Message}");
            }
        }
    }

    public virtual void UnregisterServices(IEnumerable<IActionService> actionServices)
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
    }

    protected void EnsureMaskCapacity(int id)
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

    public virtual void RegisterAction<TAction, TTarget>(
        ActionService<TAction, TTarget> actionService)
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

        var reusableContext = new SystemContext<TAction, TTarget>
        {
            Dispatcher = this,
            Service = actionService
        };

        ComponentActionEntity2<TAction, TTarget, SystemContext<TAction, TTarget>> forEachDelegate = (SystemContext<TAction, TTarget> ctxState, Entity entity, ref TAction action, ref TTarget target) =>
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
                ctxState.Service.InternalExecute(action, ref target, ctx);

                ctxState.World.RemoveComponent<TAction>(entity);
                if (hasPendingPrediction) ctxState.World.RemoveComponent<ActionPendingPrediction>(entity);
            }
            finally
            {
                ctxState.World.CurrentActionPredictionId = savedPid;
            }
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

    private class SystemContext<TAction, TTarget>
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        public EntityWorld World;
        public Dispatcher Dispatcher;
        public ActionService<TAction, TTarget> Service;
    }

    public bool IsActionEnabled(IActionService actionService)
    {
        return actionService.RuntimeId != -1 &&
               actionService.RuntimeId < _executionMask.Length &&
               _executionMask[actionService.RuntimeId];
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

    protected void EnsureActionRunnersRegistered<TAction>()
        where TAction : struct, IAction
    {
        var id = ComponentId<TAction>.DenseId;
        if (_registeredActionDenseIds.Contains(id)) return;

        void Runner(TAction action, EntityWorld state, Entity entity)
        {
            state.AddComponent(entity, action);
        }

        void ByteRunner(byte[] buffer, int offset, EntityWorld state, Entity entity)
        {
            var span = new ReadOnlySpan<byte>(buffer, offset, System.Runtime.CompilerServices.Unsafe.SizeOf<TAction>());
            var action = MemoryMarshal.Read<TAction>(span);
            state.AddComponent(entity, action);
        }

        _actionRunners[id] = (Action<TAction, EntityWorld, Entity>)Runner;
        _byteActionRunners[id] = ByteRunner;
        _registeredActionDenseIds.Add(id);
    }

    private static int _actionPendingPredictionDenseId = -1;

    internal static int GetActionPendingPredictionDenseId()
    {
        if (_actionPendingPredictionDenseId != -1) return _actionPendingPredictionDenseId;
        try
        {
            _actionPendingPredictionDenseId = ComponentId<ActionPendingPrediction>.IntId;
        }
        catch
        {
            _actionPendingPredictionDenseId = -2;
        }
        return _actionPendingPredictionDenseId;
    }
}
