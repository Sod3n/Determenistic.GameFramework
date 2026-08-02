namespace Deterministic.GameFramework.DAR;

public class ReactionRunnerDisposable(ReactionDispatcher dispatcher, IEnumerable<IReactionService>? servicesToDisable) : IDisposable
{
    private IEnumerable<IReactionService>? _servicesToDisable = servicesToDisable;

    public void Dispose()
    {
        if (_servicesToDisable == null) return;

        dispatcher.DisableReactions(_servicesToDisable);
        _servicesToDisable = null;
    }
}
