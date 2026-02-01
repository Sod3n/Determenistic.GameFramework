using System.Collections.Generic;
using Newtonsoft.Json;
using Deterministic.GameFramework.Core;

/// <summary>
/// Interface for all DAR actions, providing common action operations.
/// </summary>
public interface IDARAction
{
	List<IHasPrepareReaction> PrepareReactions { get; set; }
	List<IHasAbortReaction> AbortReactions { get; set; }
	List<IHasBeforeReaction> BeforeReactions { get; set; }
	List<IHasAfterReaction> AfterReactions { get; set; }
	IDomain Source { get; set; }
	
	System.Type GetTargetType();
	bool IsExecutable(IDomain domain);
	bool IsRevertable(IDomain domain);
	void RevertProcess(IDomain domain);
	IDARAction Execute(IDomain domain);
	string GetActionType();
}

/// <summary>
/// Base generic action class that targets a specific domain type.
/// Provides compile-time type safety for domain operations.
/// 
/// Reaction execution order:
/// 1. Prepare - Modify action parameters before validation
/// 2. Abort - Check if action should be aborted (returns bool)
/// 3. Before - Execute logic before the main action
/// 4. ExecuteProcess - The main action logic
/// 5. After - Execute logic after the main action
/// </summary>
public abstract class DARAction<TDomain, TAction> : IDARAction where TDomain : class, IDomain where TAction : class, IDARAction
{
	// Reactions are collected at execution time from domain tree
	[JsonIgnore] public List<IHasPrepareReaction> PrepareReactions { get; set; } = new();
	[JsonIgnore] public List<IHasAbortReaction> AbortReactions { get; set; } = new();
	[JsonIgnore] public List<IHasBeforeReaction> BeforeReactions { get; set; } = new();
	[JsonIgnore] public List<IHasAfterReaction> AfterReactions { get; set; } = new();

	[JsonIgnore] public IDomain Source { get; set; }

	public System.Type GetTargetType() => typeof(TDomain);

	public bool IsExecutable(IDomain domainRoot)
	{
		var domain = domainRoot?.GetFirst<TDomain>(true);
		if (domain == null) return false;
		
		var domainInterface = domain as IDomain;
		if (domainInterface == null) return false;
		
		// Clear and collect prepare reactions
		PrepareReactions.Clear();
		domainInterface.CollectPrepareAll(this);
		
		// Run prepare reactions - they can modify action parameters
		foreach (var reaction in PrepareReactions)
		{
			ExecutionLogger.Current?.LogPrepareReaction(
				reaction.GetType().Name, (domainInterface as LeafDomain)!);
			reaction.InvokePrepare(domainInterface, this);
		}
		
		// Clear and collect abort reactions
		AbortReactions.Clear();
		domainInterface.CollectAbortAll(this);

		// Run abort reactions - if any returns true, action is aborted
		foreach (var reaction in AbortReactions)
		{
			ExecutionLogger.Current?.LogAbortReaction(
				reaction.GetType().Name, (domainInterface as LeafDomain)!);
			if (reaction.InvokeAbort(domainInterface, this))
			{
				return false;
			}
		}

		return _IsExecutable(domain);
	}

	public bool IsRevertable(IDomain domain)
	{
		var typedDomain = domain?.GetFirst<TDomain>(true);
		if (typedDomain != null)
		{
			return _IsRevertable(typedDomain);
		}
		return false;
	}

	public TAction From(IDomain source)
	{
		Source = source;
		return (this as TAction)!;
	}

	public virtual TAction Execute(IDomain domainRoot)
	{
		// Clear reaction arrays before collecting
		BeforeReactions.Clear();
		AfterReactions.Clear();

		var domain = domainRoot?.GetFirst<TDomain>(true);
		var domainInterface = domain as IDomain;

		if (!IsExecutable(domainRoot))
		{
			// Action not executable
			return (this as TAction)!;
		}

		// Log action start
		ExecutionLogger.Current?.LogActionStart(
			GetType().Name, domainInterface as LeafDomain);

		// Collect reactions from domain hierarchy
		domainInterface!.CollectBeforeAll(this);
		domainInterface.CollectAfterAll(this);

		// Execute before reactions
		foreach (var reaction in BeforeReactions)
		{
			ExecutionLogger.Current?.LogBeforeReaction(
				reaction.GetType().Name, domainInterface as LeafDomain);
			reaction.InvokeBefore(domainInterface, this);
		}

		// Execute the action
		ExecutionLogger.Current?.LogActionExecute(
			GetType().Name, domainInterface as LeafDomain);
		ExecuteProcess(domain!);

		// Execute after reactions
		foreach (var reaction in AfterReactions)
		{
			ExecutionLogger.Current?.LogAfterReaction(
						reaction.GetType().Name, domainInterface as LeafDomain);
			reaction.InvokeAfter(domainInterface, this);
		}

		// Log action end
		ExecutionLogger.Current?.LogActionEnd(
			GetType().Name, domainInterface as LeafDomain);

		return (this as TAction)!;
	}

	public TAction Revert(BranchDomain domain)
	{
		domain.RevertAction(this);
		return (this as TAction)!;
	}

	protected virtual bool _IsExecutable(TDomain domain) => true;
	protected virtual bool _IsRevertable(TDomain domain) => false;
	
	protected abstract void ExecuteProcess(TDomain domain);

	protected virtual void RevertProcess(TDomain domain) { }

	// Explicit interface implementations
	void IDARAction.RevertProcess(IDomain domain)
	{
		var typedDomain = domain?.GetFirst<TDomain>(true);
		if (typedDomain == null) return;
		
		RevertProcess(typedDomain);
	}

	IDARAction IDARAction.Execute(IDomain domain)
	{
		return Execute(domain);
	}

	public string GetActionType()
	{
		return GetType().Name;
	}

}