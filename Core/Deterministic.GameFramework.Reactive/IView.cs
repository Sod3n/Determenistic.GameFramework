namespace Deterministic.GameFramework.Reactive;

public interface IView<T> where T : ViewModel
{
    
    void Initialize(T viewModel);
}