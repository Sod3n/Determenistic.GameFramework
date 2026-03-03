# Benchmark Results (M2 Pro, .NET 8)

## ECS Benchmark (100,000 Entities)
| Method | Mean | Error | StdDev | Allocated |
|------- |-----:|------:|-------:|----------:|
| ForEach_Struct_Ref | 239.8 μs | 4.56 μs | 4.48 μs | - |
| Manual_Iteration | 144.7 μs | 2.65 μs | 2.22 μs | - |

*   **Analysis**: `ForEach` introduces ~65% overhead compared to raw array iteration, primarily due to delegate invocation and safety checks. However, ~240μs for 100k entities is still highly performant (approx 0.0024μs per entity).

## Action Dispatcher Benchmark
| Method | Mean | Median | Gen0 | Allocated |
|------- |-----:|-------:|-----:|----------:|
| Execute_Action | 29.408 ns | 28.514 ns | 0.0029 | 24 B |
| GetDenseId | 7.806 ns | 7.765 ns | - | - |

*   **Analysis**: Action dispatching is extremely lightweight (~30ns). The 24B allocation per call is likely the `Context` object or closure capture. `GetDenseId` is effectively free (~8ns).

## Serialization Benchmark (10,000 Entities)
| Method | Mean | Error | StdDev | Allocated |
|------- |-----:|------:|-------:|----------:|
| Serialize | 205.4 μs | 3.49 μs | 2.91 μs | 647.71 KB |
| Deserialize | 103.3 μs | 1.40 μs | 1.09 μs | 256.07 KB |

*   **Analysis**:
    *   **Speed**: Excellent. Full state serialization for 10k entities takes only ~0.2ms. Deserialization is even faster at ~0.1ms.
    *   **Memory**: High allocations (647KB serialize, 256KB deserialize). This indicates significant object/array creation during the process (e.g., new byte arrays, boxing). Future optimization could focus on buffer pooling (`ArrayPool<byte>`) to reduce GC pressure.
