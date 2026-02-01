using System.Security.Cryptography;
using System.Text;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Extensions;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Validates determinism by running a shadow simulation alongside the primary.
/// Compares execution logs (action/reaction order) after each network action completes.
/// 
/// IMPORTANT: The shadow GameState must NOT be added to the ServerDomain tree.
/// It runs in isolation, receiving only the serialized network actions.
/// This mimics how a client would replay actions.
/// 
/// Enable via DeterminismValidator.IsEnabled = true before creating matches.
/// </summary>
public class DeterminismValidator<TGameState> where TGameState : NetworkGameState
{
    /// <summary>
    /// Global flag to enable/disable determinism validation.
    /// Should be set before creating matches.
    /// </summary>
    public static bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// When true, uses fast hash-based state comparison.
    /// When false, uses full JSON comparison (slower but provides detailed diff).
    /// </summary>
    public static bool UseFastStateComparison { get; set; } = true;
    
    /// <summary>
    /// When true, skips state snapshot validation entirely (fastest).
    /// Only validates event sequences. Use for maximum performance in production.
    /// </summary>
    public static bool SkipStateValidation { get; set; } = true;
    
    /// <summary>
    /// Called when a determinism failure is detected.
    /// Parameters: (matchId, actionType, actionJson, primaryLog, shadowLog, firstMismatchIndex)
    /// </summary>
    public static event Action<Guid, string, string, string, string, int>? OnDeterminismFailure;
    
    private readonly TGameState _primary;
    private readonly TGameState _shadow;
    private readonly Guid _matchId;
    private readonly NetworkActionExecutor _shadowExecutor;
    private int _actionCount = 0;
    private bool _hasFailed = false;
    
    public TGameState Primary => _primary;
    public TGameState Shadow => _shadow;
    public bool HasFailed => _hasFailed;
    public int ActionCount => _actionCount;
    
    /// <summary>
    /// Creates a determinism validator with primary and shadow game states.
    /// Both states are initialized with the same matchId (same random seed).
    /// </summary>
    public DeterminismValidator(TGameState primary, TGameState shadow, Guid matchId)
    {
        _primary = primary;
        _shadow = shadow;
        _matchId = matchId;
        _shadowExecutor = new NetworkActionExecutor(_shadow.Registry);
        
        Console.WriteLine($"[DeterminismValidator] Initialized for match {matchId}");
    }
    
    /// <summary>
    /// Validates an action by executing it on both primary and shadow with execution logging.
    /// Call this to wrap the execution of a network action.
    /// </summary>
    /// <param name="action">The action to execute and validate</param>
    /// <param name="executePrimary">Callback to execute the action on primary</param>
    /// <returns>True if validation passed</returns>
    public bool ValidateAction(INetworkAction action, Action executePrimary)
    {
        if (_hasFailed)
        {
            executePrimary();
            return true; // Don't block primary, just skip validation
        }
        
        _actionCount++;
        
        try
        {
            // Clone the action via JSON serialization
            var actionJson = JsonSerializer.ToJson(action);
            var clonedAction = JsonSerializer.FromJson<INetworkAction>(actionJson);
            
            if (clonedAction == null)
            {
                Console.WriteLine($"[DeterminismValidator] Warning: Failed to clone action {action.GetType().Name}");
                executePrimary();
                return true;
            }
            
            // Execute shadow with logging
            var shadowLogger = ExecutionLogger.BeginLogging();
            _shadowExecutor.ExecuteAction(clonedAction, action.ExecutorId, 
                error => Console.WriteLine($"[DeterminismValidator] Shadow execution error: {error}"));
            var shadowLogs = shadowLogger.Logs.ToList();
            ExecutionLogger.EndLogging();
            
            // Execute primary with logging
            var primaryLogger = ExecutionLogger.BeginLogging();
            executePrimary();
            var primaryLogs = primaryLogger.Logs.ToList();
            ExecutionLogger.EndLogging();
            
            // Compare execution logs
            Console.WriteLine($"[DeterminismValidator] Comparing logs: Primary={primaryLogs.Count} events, Shadow={shadowLogs.Count} events");
            var mismatchIndex = CompareExecutionLogs(primaryLogs, shadowLogs);
            if (mismatchIndex >= 0)
            {
                LogEventSequenceFailure(action, actionJson, primaryLogs, shadowLogs, mismatchIndex);
                _hasFailed = true;
                return false;
            }
            else
            {
                Console.WriteLine($"[DeterminismValidator] Logs match - checking state snapshots...");
            }
            
            // Compare state snapshots
            if (!SkipStateValidation)
            {
                if (UseFastStateComparison)
                {
                    // Fast: Compare hash of serialized state
                    var primaryHash = GetStateHash(_primary);
                    var shadowHash = GetStateHash(_shadow);
                    
                    if (primaryHash != shadowHash)
                    {
                        // Hash mismatch - do full comparison for detailed error
                        var primaryStateJson = JsonSerializer.ToJson(_primary);
                        var shadowStateJson = JsonSerializer.ToJson(_shadow);
                        LogStateSnapshotFailure(action, actionJson, primaryStateJson, shadowStateJson);
                        _hasFailed = true;
                        return false;
                    }
                }
                else
                {
                    // Slow: Full JSON string comparison (for debugging)
                    var primaryStateJson = JsonSerializer.ToJson(_primary);
                    var shadowStateJson = JsonSerializer.ToJson(_shadow);
                    
                    if (primaryStateJson != shadowStateJson)
                    {
                        LogStateSnapshotFailure(action, actionJson, primaryStateJson, shadowStateJson);
                        _hasFailed = true;
                        return false;
                    }
                }
                
                Console.WriteLine($"[DeterminismValidator] State snapshots match - validation passed");
            }
            else
            {
                Console.WriteLine($"[DeterminismValidator] State validation skipped - validation passed");
            }
            
            // Periodic logging for long-running matches
            if (_actionCount % 100 == 0)
            {
                Console.WriteLine($"[DeterminismValidator] Match {_matchId}: {_actionCount} actions validated, {primaryLogs.Count} execution events");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DeterminismValidator] Exception during validation: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            _hasFailed = true;
            return false;
        }
    }
    
    /// <summary>
    /// Compares execution logs and returns the index of the first mismatch, or -1 if they match.
    /// </summary>
    private int CompareExecutionLogs(List<ExecutionLog> primaryLogs, List<ExecutionLog> shadowLogs)
    {
        var maxCount = Math.Max(primaryLogs.Count, shadowLogs.Count);
        
        for (int i = 0; i < maxCount; i++)
        {
            if (i >= primaryLogs.Count)
            {
                return i; // Shadow has more events
            }
            if (i >= shadowLogs.Count)
            {
                return i; // Primary has more events
            }
            
            if (!primaryLogs[i].Matches(shadowLogs[i]))
            {
                return i; // Events don't match
            }
        }
        
        return -1; // All match
    }
    
    private void LogEventSequenceFailure(INetworkAction action, string actionJson, 
        List<ExecutionLog> primaryLogs, List<ExecutionLog> shadowLogs, int mismatchIndex)
    {
        var actionType = action.GetType().Name;
        
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        DETERMINISM FAILURE: EVENT SEQUENCE MISMATCH          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Match:      {_matchId}");
        Console.WriteLine($"║ Action #:   {_actionCount}");
        Console.WriteLine($"║ Action:     {actionType}");
        Console.WriteLine($"║ Mismatch:   Event #{mismatchIndex}");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        
        // Show context around mismatch
        var contextStart = Math.Max(0, mismatchIndex - 3);
        var contextEnd = Math.Min(Math.Max(primaryLogs.Count, shadowLogs.Count), mismatchIndex + 3);
        
        Console.WriteLine("║ PRIMARY EXECUTION:");
        for (int i = contextStart; i < contextEnd && i < primaryLogs.Count; i++)
        {
            var marker = i == mismatchIndex ? ">>> " : "    ";
            Console.WriteLine($"║ {marker}{primaryLogs[i]}");
        }
        if (mismatchIndex >= primaryLogs.Count)
        {
            Console.WriteLine($"║ >>> [MISSING EVENT #{mismatchIndex}]");
        }
        
        Console.WriteLine("║");
        Console.WriteLine("║ SHADOW EXECUTION:");
        for (int i = contextStart; i < contextEnd && i < shadowLogs.Count; i++)
        {
            var marker = i == mismatchIndex ? ">>> " : "    ";
            Console.WriteLine($"║ {marker}{shadowLogs[i]}");
        }
        if (mismatchIndex >= shadowLogs.Count)
        {
            Console.WriteLine($"║ >>> [MISSING EVENT #{mismatchIndex}]");
        }
        
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        
        // Format logs for event
        var primaryLogStr = string.Join("\n", primaryLogs.Select(l => l.ToString()));
        var shadowLogStr = string.Join("\n", shadowLogs.Select(l => l.ToString()));
        
        // Fire event for external handling (logging, crash reporting, etc.)
        OnDeterminismFailure?.Invoke(_matchId, actionType, actionJson, primaryLogStr, shadowLogStr, mismatchIndex);
    }
    
    private void LogStateSnapshotFailure(INetworkAction action, string actionJson,
        string primaryStateJson, string shadowStateJson)
    {
        var actionType = action.GetType().Name;
        
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        DETERMINISM FAILURE: STATE SNAPSHOT MISMATCH          ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine($"║ Match:      {_matchId}");
        Console.WriteLine($"║ Action #:   {_actionCount}");
        Console.WriteLine($"║ Action:     {actionType}");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        Console.WriteLine("║ Event sequences matched, but state diverged!");
        Console.WriteLine("║ This indicates non-deterministic state mutation.");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        
        // Find first difference in JSON
        var diffIndex = FindFirstDifference(primaryStateJson, shadowStateJson);
        var contextStart = Math.Max(0, diffIndex - 50);
        var contextEnd = Math.Min(primaryStateJson.Length, diffIndex + 50);
        
        Console.WriteLine("║ PRIMARY STATE (around difference):");
        var primaryContext = primaryStateJson.Substring(contextStart, Math.Min(100, primaryStateJson.Length - contextStart));
        Console.WriteLine($"║ ...{primaryContext}...");
        
        Console.WriteLine("║");
        Console.WriteLine("║ SHADOW STATE (around difference):");
        var shadowContext = shadowStateJson.Substring(contextStart, Math.Min(100, shadowStateJson.Length - contextStart));
        Console.WriteLine($"║ ...{shadowContext}...");
        
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        
        // Fire event for external handling
        OnDeterminismFailure?.Invoke(_matchId, actionType, actionJson, primaryStateJson, shadowStateJson, diffIndex);
    }
    
    private int FindFirstDifference(string str1, string str2)
    {
        var minLength = Math.Min(str1.Length, str2.Length);
        for (int i = 0; i < minLength; i++)
        {
            if (str1[i] != str2[i])
            {
                return i;
            }
        }
        return minLength; // One string is prefix of the other
    }
    
    private string GetStateHash(TGameState state)
    {
        var json = JsonSerializer.ToJson(state);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }
}
