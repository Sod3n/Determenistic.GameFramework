namespace Deterministic.GameFramework.Core;

/// <summary>
/// Records a single execution event (action or reaction) for determinism validation.
/// </summary>
public class ExecutionLog
{
    public int SequenceId { get; set; }
    public ExecutionEventType EventType { get; set; }
    public string ActionType { get; set; } = "";
    public string ReactionType { get; set; } = "";
    public int DomainId { get; set; }
    public string DomainType { get; set; } = "";
    
    public override string ToString()
    {
        return EventType switch
        {
            ExecutionEventType.ActionStart => $"[{SequenceId}] Action.Start: {ActionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.PrepareReaction => $"[{SequenceId}] Prepare: {ReactionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.AbortReaction => $"[{SequenceId}] Abort: {ReactionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.BeforeReaction => $"[{SequenceId}] Before: {ReactionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.ActionExecute => $"[{SequenceId}] Action.Execute: {ActionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.AfterReaction => $"[{SequenceId}] After: {ReactionType} on {DomainType}[{DomainId}]",
            ExecutionEventType.ActionEnd => $"[{SequenceId}] Action.End: {ActionType} on {DomainType}[{DomainId}]",
            _ => $"[{SequenceId}] Unknown"
        };
    }
    
    public bool Matches(ExecutionLog other)
    {
        return EventType == other.EventType
            && ActionType == other.ActionType
            && ReactionType == other.ReactionType
            && DomainId == other.DomainId
            && DomainType == other.DomainType;
    }
}

public enum ExecutionEventType
{
    ActionStart,
    PrepareReaction,
    AbortReaction,
    BeforeReaction,
    ActionExecute,
    AfterReaction,
    ActionEnd
}
