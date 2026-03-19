using BenchmarkDotNet.Running;

namespace Deterministic.GameFramework.Benchmarks.DAR;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<SchedulerBenchmarks>();
        BenchmarkRunner.Run<DispatcherBenchmarks>();
    }
}
