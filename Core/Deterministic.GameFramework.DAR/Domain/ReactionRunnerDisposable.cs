namespace Deterministic.GameFramework.DAR;

// TODO: Currently it doesnt use counter, so it sometimes may disable when we dont want.
public class ReactionRunnerDisposable(Dispatcher dispatcher, IEnumerable<IReactionService>? servicesToDisable) : IDisposable
{
    private IEnumerable<IReactionService>? _servicesToDisable = servicesToDisable;

    public void Dispose()
    {
        if (_servicesToDisable == null) return;
        
        dispatcher.DisableReactions(_servicesToDisable);
        _servicesToDisable = null;
    }
}