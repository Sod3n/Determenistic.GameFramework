```

BenchmarkDotNet v0.13.12, macOS 26.3 (25D125) [Darwin 25.3.0]
Apple M2 Pro, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.105
  [Host]     : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 8.0.19 (8.0.1925.36514), Arm64 RyuJIT AdvSIMD


```
| Method                    | EntityCount | Mean      | Error    | StdDev   | Gen0     | Allocated |
|-------------------------- |------------ |----------:|---------:|---------:|---------:|----------:|
| **PhysicsUpdate**             | **10**          |  **24.52 μs** | **0.126 μs** | **0.105 μs** |   **6.5613** |  **53.76 KB** |
| PhysicsUpdate_WithRebuild | 10          |  48.64 μs | 0.380 μs | 0.355 μs |  11.7798 |  96.57 KB |
| **PhysicsUpdate**             | **50**          | **107.15 μs** | **0.666 μs** | **0.590 μs** |  **31.2500** | **256.08 KB** |
| PhysicsUpdate_WithRebuild | 50          | 193.59 μs | 2.429 μs | 2.154 μs |  52.9785 | 434.55 KB |
| **PhysicsUpdate**             | **100**         | **226.32 μs** | **1.417 μs** | **1.256 μs** |  **62.5000** | **512.46 KB** |
| PhysicsUpdate_WithRebuild | 100         | 374.55 μs | 2.779 μs | 2.463 μs | 105.4688 |  862.3 KB |
