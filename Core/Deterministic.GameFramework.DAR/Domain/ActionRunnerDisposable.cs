namespace Deterministic.GameFramework.DAR;

// TODO: Currently it doesnt use counter, so it sometimes may disable when we dont want.
public class ActionRunnerDisposable(Dispatcher dispatcher, IEnumerable<IActionService>? servicesToDisable) : IDisposable
{
    private IEnumerable<IActionService>? _servicesToDisable = servicesToDisable;

    public void Dispose()
    {
        if (_servicesToDisable == null) return;
        
        dispatcher.DisableActions(_servicesToDisable);
        _servicesToDisable = null;
    }
}