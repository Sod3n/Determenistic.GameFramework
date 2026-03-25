```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-VSVGMD : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

IterationCount=10  WarmupCount=3  

```
| Method                   | Count | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **Dispatcher_Many_Entities** | **100**   |        **NA** |        **NA** |        **NA** |     **?** |       **?** |        **NA** |           **?** |
| ECS_System_Many_Entities | 100   |  1.481 μs | 0.0885 μs | 0.0585 μs |     ? |       ? |         - |           ? |
|                          |       |           |           |           |       |         |           |             |
| **Dispatcher_Many_Entities** | **1000**  |        **NA** |        **NA** |        **NA** |     **?** |       **?** |        **NA** |           **?** |
| ECS_System_Many_Entities | 1000  | 12.796 μs | 0.5967 μs | 0.3551 μs |     ? |       ? |         - |           ? |

Benchmarks with issues:
  ActionEcsBenchmark.Dispatcher_Many_Entities: Job-VSVGMD(IterationCount=10, WarmupCount=3) [Count=100]
  ActionEcsBenchmark.Dispatcher_Many_Entities: Job-VSVGMD(IterationCount=10, WarmupCount=3) [Count=1000]
