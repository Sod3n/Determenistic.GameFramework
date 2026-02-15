using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Core;

public class GameLoop : BranchDomain
{
	private readonly BranchDomain _rootDomain;
	
	private readonly ConcurrentQueue<Action> _scheduledActions = new();
	private readonly ConcurrentDictionary<long, ConcurrentQueue<Action>> _tickScheduledActions = new();
	
	// Track active processors in the domain tree
	private readonly HashSet<IProcessor> _enabledProcessors = new();

	private bool _isRunning;
	private readonly Stopwatch _stopwatch = new();
	private long _lastFrameTicks;
	
	// Fixed tick rate
	private int _tickRate = 60;
	private float _fixedDeltaTime = 1f / 60f;
	private float _accumulator;
	
	// Cap max ticks per frame to prevent spiral of death
	private const int MaxTicksPerFrame = 5;
	
	/// <summary>
	/// Number of ticks to delay before the loop starts advancing.
	/// Used on the client to give server actions time to arrive.
	/// </summary>
	public int TickDelay { get; set; }
	
	/// <summary>
	/// The fixed delta time each tick advances by (1 / tickRate).
	/// </summary>
	public float FixedDeltaTime => _fixedDeltaTime;
	
	/// <summary>
	/// The current tick count since the loop started.
	/// </summary>
	public long CurrentTick { get; private set; }
	
	public int TickRate => _tickRate;
	
	public event Action? OnUpdate;

	public GameLoop(BranchDomain rootDomain) : base(rootDomain)
	{
		_rootDomain = rootDomain;
		
		// Auto-inject tick info into any action implementing IRequireTick
		new Reaction<LeafDomain, IRequireTick>(rootDomain)
			.Prepare((_, action) =>
			{
				action.CurrentTick = CurrentTick;
				action.TickRate = _tickRate;
			})
			.AddTo(Disposables);
	}

	public void SetTickRate(int tickRate)
	{
		_tickRate = tickRate;
		_fixedDeltaTime = 1f / tickRate;
	}
	
	/// <summary>
	/// Backward-compatible alias for SetTickRate.
	/// </summary>
	public void SetTargetFps(int fps) => SetTickRate(fps);
	
	public Task Start()
	{
		return Task.Run(() =>
		{
			try
			{
				_isRunning = true;
				_fixedDeltaTime = 1f / _tickRate;
				_accumulator = -(TickDelay * _fixedDeltaTime);
				CurrentTick = 0;
				
				_stopwatch.Start();
				_lastFrameTicks = _stopwatch.ElapsedTicks;
				
				Console.WriteLine($"[GameLoop] Started at {_tickRate} tick/s (fixed dt={_fixedDeltaTime:F4}s)");
				
				int targetFrameTimeMs = 1000 / _tickRate;
				
				while (_isRunning)
				{
					try
					{
						// Measure real elapsed time since last frame
						long currentTicks = _stopwatch.ElapsedTicks;
						long deltaTicks = currentTicks - _lastFrameTicks;
						float elapsed = (float)deltaTicks / Stopwatch.Frequency;
						_lastFrameTicks = currentTicks;
						
						// Accumulate real time, then consume in fixed-step ticks
						_accumulator += elapsed;
						
						// Execute scheduled actions once per frame (before ticks)
						DrainScheduledActions();
						
						// Run as many fixed ticks as the accumulator allows
						int ticksThisFrame = 0;
						while (_accumulator >= _fixedDeltaTime && ticksThisFrame < MaxTicksPerFrame)
						{
							Tick(_fixedDeltaTime);
							_accumulator -= _fixedDeltaTime;
							CurrentTick++;
							ticksThisFrame++;
						}
						
						// If we hit the cap, discard leftover to prevent spiral of death
						if (ticksThisFrame >= MaxTicksPerFrame && _accumulator > _fixedDeltaTime)
						{
							_accumulator = 0f;
						}
						
						// Sleep for remaining time to avoid busy-waiting
						long frameEndTicks = _stopwatch.ElapsedTicks;
						long frameDurationTicks = frameEndTicks - currentTicks;
						int frameDurationMs = (int)(frameDurationTicks * 1000 / Stopwatch.Frequency);
						
						int sleepTime = Math.Max(0, targetFrameTimeMs - frameDurationMs);
						if (sleepTime > 0)
						{
							System.Threading.Thread.Sleep(sleepTime);
						}
					}
					catch (Exception ex)
					{
						// Critical: catch frame-level errors to prevent loop crash
						Console.WriteLine($"[GameLoop] CRITICAL ERROR in frame update: {ex.Message}");
						Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
						// Continue running - don't let one bad frame kill the server
					}
				}
				
				Console.WriteLine("[GameLoop] Stopped gracefully");
			}
			catch (Exception ex)
			{
				// Catastrophic error - log and notify
				Console.WriteLine($"[GameLoop] FATAL ERROR - Loop crashed: {ex.Message}");
				Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
				_isRunning = false;
				throw; // Re-throw to notify calling code
			}
		});
	}
	
	public void Stop() => _isRunning = false;
	
	/// <summary>
	/// Synchronously advance the game loop from CurrentTick to targetTick.
	/// Runs all tick-scheduled actions and processors for each tick.
	/// Used during state sync / replay to fast-forward deterministically.
	/// </summary>
	public void AdvanceToTick(long targetTick)
	{
		while (CurrentTick < targetTick)
		{
			DrainScheduledActions();
			Tick(_fixedDeltaTime);
			CurrentTick++;
		}
	}
	
	public void Schedule(Action action)
	{
		if (!_isRunning) return;
		_scheduledActions.Enqueue(action);
	}
	
	/// <summary>
	/// Schedule an action to execute at the start of a specific tick.
	/// If the tick has already passed, the action executes on the next tick.
	/// </summary>
	public void ScheduleOnTick(long tick, Action action)
	{
		var queue = _tickScheduledActions.GetOrAdd(tick, _ => new ConcurrentQueue<Action>());
		queue.Enqueue(action);
	}
	
	private void DrainTickScheduledActions(long tick)
	{
		if (_tickScheduledActions.TryRemove(tick, out var queue))
		{
			while (queue.TryDequeue(out var action))
			{
				try
				{
					action.Invoke();
				}
				catch (Exception ex)
				{
					Console.WriteLine($"[GameLoop] Error in tick-scheduled action (tick {tick}): {ex.Message}");
					Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
				}
			}
		}
	}
	
	private void DrainScheduledActions()
	{
		while (_scheduledActions.TryDequeue(out var action))
		{
			try
			{
				action.Invoke();
			}
			catch (Exception ex)
			{
				// Isolate errors - one action failure doesn't crash the loop
				Console.WriteLine($"[GameLoop] Error in scheduled action: {ex.Message}");
				Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
			}
		}
	}
	
	private void Tick(float delta)
	{
		// Execute actions scheduled for this tick
		DrainTickScheduledActions(CurrentTick);
		
		// Notify listeners with error handling
		try
		{
			OnUpdate?.Invoke();
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GameLoop] Error in OnUpdate listener: {ex.Message}");
			Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
		}
		
		// Process all processors from domain tree with error handling
		try
		{
			ProcessAllProcessors(delta);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[GameLoop] Error in ProcessAllProcessors: {ex.Message}");
			Console.WriteLine($"[GameLoop] Stack trace: {ex.StackTrace}");
		}
	}

	private void ProcessAllProcessors(float delta)
	{
		// Discover current processors from the domain tree
		// Include root domain in search
		var currentList = _rootDomain.GetAll<IProcessor>(includeSelf: true);
		var currentProcessors = new HashSet<IProcessor>(currentList);
		
		// Disable processors that were removed from the tree
		var toDisable = new List<IProcessor>();
		foreach (var processor in _enabledProcessors)
		{
			if (!currentProcessors.Contains(processor))
			{
				toDisable.Add(processor);
			}
		}
		
		foreach (var processor in toDisable)
		{
			_enabledProcessors.Remove(processor);
			processor.OnProcessorDisabled();
		}
		
		// Enable new processors that appeared in the tree
		foreach (var processor in currentProcessors)
		{
			if (_enabledProcessors.Add(processor))
			{
				processor.OnProcessorEnabled();
			}
		}
		
		// Process all current processors
		foreach (var processor in currentProcessors)
		{
			processor.Process(delta, CurrentTick);
		}
	}
}
