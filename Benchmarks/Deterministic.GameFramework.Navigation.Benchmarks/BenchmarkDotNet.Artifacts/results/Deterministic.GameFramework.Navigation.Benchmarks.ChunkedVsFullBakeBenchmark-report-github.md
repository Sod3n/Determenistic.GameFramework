```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-OZOPMB : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

Runtime=.NET 8.0  InvocationCount=1  IterationCount=10  
UnrollFactor=1  WarmupCount=3  

```
| Method                   | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| ChunkedBake_ObstacleMove |  8.087 ms |  2.844 ms |  1.881 ms |  8.293 ms |  1.00 |    0.00 |  41.72 KB |        1.00 |
| FullBake_ObstacleMove    | 32.052 ms | 33.039 ms | 21.853 ms | 21.222 ms |  4.36 |    3.27 |  15.19 KB |        0.36 |
