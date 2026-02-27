using Deterministic.GameFramework.CoreV2;

namespace Shared;

[NetworkId(1)]
public struct PlayerComponent : IComponent
{
    public FixedString32 Name;
    public Int Score;
}

[NetworkId(100)]
public struct IncrementScoreAction : IAction
{
    public int Amount;
}

[NetworkId(200)]
public struct SpawnPlayerAction : IAction
{
    public FixedString32 PlayerName;
    // We could pass the Player Guid here if we wanted to map it back
}

[NetworkId(101)]
public class IncrementScoreService : ActionService<IncrementScoreAction, PlayerComponent>
{
    protected override void ExecuteProcess(IncrementScoreAction args, ref PlayerComponent target, Context ctx)
    {
        target.Score += args.Amount;
        Console.WriteLine($"[Service] Player {target.Name} score: {target.Score}");
    }
}

[NetworkId(201)]
public class SpawnPlayerService : ActionService<SpawnPlayerAction, PlayerComponent>
{
    // Note: ActionService<TAction, TTarget> usually expects to operate on an EXISTING entity with TTarget.
    // For spawning, we often use a "Singleton" or "GameManager" entity as the target, 
    // OR we use a special "GlobalActionService" (not yet implemented in this simplified example).
    // WORKAROUND: We will register this service for a dummy component, but we will ignore the target
    // and Create a NEW entity in the context.
    
    // Actually, ActionService.ExecuteProcess provides 'Context ctx' which has 'State'. 
    // We can CreateEntity on the State. 
    // BUT, the dispatcher requires a TargetEntity to route the action to.
    
    // BETTER APPROACH for this example: 
    // We'll target Entity 0 (which we'll assume is a 'System' entity created at match start)
    // and Spawn a NEW entity for the player.
    
    protected override void ExecuteProcess(SpawnPlayerAction args, ref PlayerComponent target, Context ctx)
    {
        // 'target' here is the System/GameManager entity's component (if we use one).
        // For now, let's just ignore 'target' and spawn a new player.
        
        var newEntity = ctx.State.CreateEntity();
        ctx.State.AddComponent(newEntity, new PlayerComponent { Name = args.PlayerName, Score = 0 });
        
        Console.WriteLine($"[SpawnService] Spawned Player {args.PlayerName} (Entity {newEntity.Id})");
    }
}
