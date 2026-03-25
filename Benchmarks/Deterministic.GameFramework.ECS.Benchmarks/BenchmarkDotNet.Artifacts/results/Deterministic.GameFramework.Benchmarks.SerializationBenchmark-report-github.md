```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-VSVGMD : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

IterationCount=10  WarmupCount=3  

```
| Method                    | Mean        | Error     | StdDev    | Gen0     | Gen1     | Gen2     | Allocated   |
|-------------------------- |------------:|----------:|----------:|---------:|---------:|---------:|------------:|
| Serialize_10k             |   267.61 μs |  3.886 μs |  2.032 μs |  71.7773 |  70.8008 |  68.3594 |  2209.22 KB |
| Deserialize_10k_FullSync  |   109.98 μs |  1.074 μs |  0.639 μs |  70.5566 |  59.5703 |  58.3496 |  1029.67 KB |
| Deserialize_10k_Rollback  |    89.21 μs |  0.732 μs |  0.436 μs |  35.6445 |  34.4238 |  33.8135 |    557.2 KB |
| Serialize_100k            | 2,837.42 μs | 69.800 μs | 41.537 μs | 519.5313 | 519.5313 | 500.0000 | 22072.79 KB |
| Deserialize_100k_FullSync | 1,407.19 μs | 25.329 μs | 16.754 μs | 337.8906 | 337.8906 | 328.1250 | 10258.96 KB |
| Deserialize_100k_Rollback |   903.53 μs | 12.893 μs |  8.528 μs | 273.4375 | 273.4375 | 263.6719 |  5567.86 KB |
