namespace Deterministic.GameFramework.Core;

public interface IRequireTick : IDARAction
{
	long CurrentTick { get; set; }
	int TickRate { get; set; }
}
