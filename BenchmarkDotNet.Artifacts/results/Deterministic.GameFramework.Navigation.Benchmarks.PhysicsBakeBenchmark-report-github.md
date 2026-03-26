```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-NFZOMQ : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

Runtime=.NET 8.0  IterationCount=5  WarmupCount=2  

```
| Method        | Mean          | Error       | StdDev     | Ratio    | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------- |--------------:|------------:|-----------:|---------:|--------:|-------:|----------:|------------:|
| SteadyState   |      5.153 μs |   0.1566 μs |  0.0242 μs |     1.00 |    0.00 | 0.0305 |     272 B |        1.00 |
| PhysicsRebake | 14,663.958 μs | 152.4617 μs | 23.5936 μs | 2,845.72 |   17.27 |      - |    6652 B |       24.46 |
