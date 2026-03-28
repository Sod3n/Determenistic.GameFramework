```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-FYCHTZ : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

Runtime=.NET 8.0  IterationCount=5  WarmupCount=2  

```
| Method          | AgentCount | Mean      | Error     | StdDev    | Gen0   | Allocated |
|---------------- |----------- |----------:|----------:|----------:|-------:|----------:|
| **AllAgentsRepath** | **1**          |  **5.367 μs** | **0.0321 μs** | **0.0050 μs** | **0.0381** |     **328 B** |
| **AllAgentsRepath** | **10**         |  **8.338 μs** | **0.0708 μs** | **0.0184 μs** | **0.0305** |     **328 B** |
| **AllAgentsRepath** | **50**         | **23.137 μs** | **0.3030 μs** | **0.0469 μs** | **0.0305** |     **328 B** |
