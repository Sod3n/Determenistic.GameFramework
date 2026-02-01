namespace Deterministic.GameFramework.Core;

/// <summary>
/// Collects execution logs for determinism validation.
/// Thread-safe singleton that can be accessed from anywhere in the action execution flow.
/// </summary>
public class ExecutionLogger
{
    [ThreadStatic]
    private static ExecutionLogger? _current;
    
    public static ExecutionLogger? Current => _current;
    
    private readonly List<ExecutionLog> _logs = new();
    private int _sequenceId = 0;
    
    public IReadOnlyList<ExecutionLog> Logs => _logs;
    
    public static ExecutionLogger BeginLogging()
    {
        _current = new ExecutionLogger();
        return _current;
    }
    
    public static void EndLogging()
    {
        _current = null;
    }
    
    public void LogActionStart(string actionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.ActionStart,
            ActionType = actionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogPrepareReaction(string reactionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.PrepareReaction,
            ReactionType = reactionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogAbortReaction(string reactionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.AbortReaction,
            ReactionType = reactionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogBeforeReaction(string reactionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.BeforeReaction,
            ReactionType = reactionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogActionExecute(string actionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.ActionExecute,
            ActionType = actionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogAfterReaction(string reactionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.AfterReaction,
            ReactionType = reactionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void LogActionEnd(string actionType, LeafDomain domain)
    {
        _logs.Add(new ExecutionLog
        {
            SequenceId = _sequenceId++,
            EventType = ExecutionEventType.ActionEnd,
            ActionType = actionType,
            DomainId = domain.Id,
            DomainType = domain.GetType().Name
        });
    }
    
    public void Clear()
    {
        _logs.Clear();
        _sequenceId = 0;
    }
}
