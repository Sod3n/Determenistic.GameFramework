using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Core;

public class GameLoop(BranchDomain rootDomain)
{
	private readonly ConcurrentQueue<Action> _scheduledActions = new();
	
	// Track active processors in the domain tree
	private readonly HashSet<IProcessor> _enabledProcessors = new();

	private bool _isRunning;
	private readonly Stopwatch _stopwatch = new();
	private long _lastFrameTicks;
	
	// Target frame rate
	private int _targetFps = 60;
	private int _targetFrameTimeMs;
	
	public event Action? OnUpdate;

	public void SetTargetFps(int fps)
	{
		_targetFps = fps;
		_targetFrameTimeMs = 1000 / fps;
	}
	
	public Task Start()
	{
		return Task.Run(() =>
		{
			try
			{
				_isRunning = true;
				_targetFrameTimeMs = 1000 / _targetFps;
				
				_stopwatch.Start();
				_lastFrameTicks = _stopwatch.ElapsedTicks;
				
				Console.WriteLine($"[GameLoop] Started at {_targetFps} FPS");
				
				while (_isRunning)
				{
					try
					{
						// Measure actual elapsed time since last frame
						long currentTicks = _stopwatch.ElapsedTicks;
						long deltaTicks = currentTicks - _lastFrameTicks;
						float deltaTime = (float)deltaTicks / Stopwatch.Frequency;
						_lastFrameTicks = currentTicks;
						
						// Update with actual delta time
						Update(deltaTime);
						
						// Sleep to maintain target frame rate
						// Calculate how long the frame took
						long frameEndTicks = _stopwatch.ElapsedTicks;
						long frameDurationTicks = frameEndTicks - currentTicks;
						int frameDurationMs = (int)(frameDurationTicks * 1000 / Stopwatch.Frequency);
						
						// Sleep for remaining time to hit target frame rate
						int sleepTime = Math.Max(0, _targetFrameTimeMs - frameDurationMs);
						if (sleepTime > 0)
						{
							// Use Thread.Sleep to stay on same thread (no context switching)
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
	
	public void Schedule(Action action)
	{
		if (!_isRunning) return;
		_scheduledActions.Enqueue(action);
	}
	
	private void Update(float delta)
	{
		// Execute scheduled actions with error isolation
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
		var currentList = rootDomain.GetAll<IProcessor>(includeSelf: true);
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
			processor.Process(delta);
		}
	}
}
