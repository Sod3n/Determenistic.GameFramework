using BenchmarkDotNet.Running;

namespace Deterministic.GameFramework.Navigation.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
