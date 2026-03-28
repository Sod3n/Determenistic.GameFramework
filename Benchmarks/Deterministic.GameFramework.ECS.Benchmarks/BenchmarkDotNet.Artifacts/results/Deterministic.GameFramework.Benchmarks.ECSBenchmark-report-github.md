```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-VSVGMD : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

IterationCount=10  WarmupCount=3  

```
| Method             | Mean     | Error    | StdDev  | Allocated |
|------------------- |---------:|---------:|--------:|----------:|
| ForEach_Struct_Ref | 431.1 μs | 14.10 μs | 9.33 μs |         - |
| Manual_Iteration   | 256.7 μs |  1.67 μs | 1.10 μs |         - |
