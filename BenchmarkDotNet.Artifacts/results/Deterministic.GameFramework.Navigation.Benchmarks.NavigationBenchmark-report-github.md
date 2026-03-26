```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD


```
| Method                         | AgentCount | RegionCount | Mean        | Error    | StdDev   | Gen0   | Gen1   | Allocated |
|------------------------------- |----------- |------------ |------------:|---------:|---------:|-------:|-------:|----------:|
| **NavigationUpdate**               | **1**          | **1**           |    **956.0 ns** |  **1.84 ns** |  **1.54 ns** | **0.0420** |      **-** |     **360 B** |
| NavigationUpdate_WithRebuild   | 1          | 1           |  2,044.1 ns |  6.14 ns |  5.74 ns | 0.3815 |      - |    3208 B |
| NavigationUpdate_TargetChanged | 1          | 1           |  1,275.1 ns |  1.79 ns |  1.68 ns | 0.0496 |      - |     416 B |
| **NavigationUpdate**               | **1**          | **4**           |  **1,188.1 ns** |  **2.17 ns** |  **2.03 ns** | **0.0496** |      **-** |     **424 B** |
| NavigationUpdate_WithRebuild   | 1          | 4           | 10,135.3 ns | 19.12 ns | 17.89 ns | 0.9003 |      - |    7648 B |
| NavigationUpdate_TargetChanged | 1          | 4           |  1,390.2 ns |  1.76 ns |  1.65 ns | 0.0572 |      - |     480 B |
| **NavigationUpdate**               | **10**         | **1**           |  **3,181.0 ns** |  **8.41 ns** |  **7.87 ns** | **0.0420** |      **-** |     **360 B** |
| NavigationUpdate_WithRebuild   | 10         | 1           |  3,738.5 ns |  3.77 ns |  2.94 ns | 1.5793 | 0.0381 |   13216 B |
| NavigationUpdate_TargetChanged | 10         | 1           |  3,525.7 ns |  3.06 ns |  2.86 ns | 0.0496 |      - |     416 B |
| **NavigationUpdate**               | **10**         | **4**           |  **4,033.5 ns** |  **6.61 ns** |  **6.18 ns** | **0.0458** |      **-** |     **424 B** |
| NavigationUpdate_WithRebuild   | 10         | 4           | 11,823.6 ns | 27.14 ns | 25.39 ns | 2.1057 | 0.0458 |   17656 B |
| NavigationUpdate_TargetChanged | 10         | 4           |  3,736.5 ns |  4.07 ns |  3.60 ns | 0.0572 |      - |     480 B |
| **NavigationUpdate**               | **50**         | **1**           | **13,171.2 ns** | **16.88 ns** | **15.79 ns** | **0.0305** |      **-** |     **360 B** |
| NavigationUpdate_WithRebuild   | 50         | 1           | 11,342.0 ns | 24.70 ns | 21.89 ns | 6.8970 | 0.6409 |   57696 B |
| NavigationUpdate_TargetChanged | 50         | 1           | 13,308.3 ns | 24.26 ns | 21.50 ns | 0.0458 |      - |     416 B |
| **NavigationUpdate**               | **50**         | **4**           | **17,699.0 ns** | **54.26 ns** | **45.31 ns** | **0.0305** |      **-** |     **424 B** |
| NavigationUpdate_WithRebuild   | 50         | 4           | 19,832.5 ns | 37.88 ns | 35.43 ns | 7.4158 | 0.7935 |   62136 B |
| NavigationUpdate_TargetChanged | 50         | 4           | 14,368.9 ns | 15.91 ns | 14.10 ns | 0.0458 |      - |     480 B |
