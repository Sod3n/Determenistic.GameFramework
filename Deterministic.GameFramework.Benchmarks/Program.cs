using BenchmarkDotNet.Running;
using Deterministic.GameFramework.Benchmarks;

namespace Deterministic.GameFramework.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
