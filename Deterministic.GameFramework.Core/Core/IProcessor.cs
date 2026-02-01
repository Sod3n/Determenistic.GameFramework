namespace Deterministic.GameFramework.Core;

// Idea that processor is a domain handler, that will contain some core logic for domain
// But it can be optionally disabled.
// For example on server it will run logic, and not on client.

public interface IProcessor
{
	void Process(float delta);
	
	// Deterministic ordering - lower values process first
	int ProcessOrder => 0;
	
	// Optional lifecycle hooks - called when processor enters/exits the processing tree
	void OnProcessorEnabled() { }
	void OnProcessorDisabled() { }
}