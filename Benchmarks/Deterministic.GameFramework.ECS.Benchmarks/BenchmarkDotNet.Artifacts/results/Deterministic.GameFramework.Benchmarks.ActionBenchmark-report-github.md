```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  Job-VSVGMD : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD

IterationCount=10  WarmupCount=3  

```
| Method         | Mean     | Error    | StdDev   | Allocated |
|--------------- |---------:|---------:|---------:|----------:|
| Execute_Action | 16.34 ns | 0.259 ns | 0.135 ns |         - |
| GetDenseId     | 10.61 ns | 0.399 ns | 0.237 ns |         - |
