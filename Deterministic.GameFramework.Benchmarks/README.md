# DAR Pattern Performance Benchmarks

This project contains performance benchmarks for the DAR (Domain-Action-Reaction) pattern using BenchmarkDotNet.

## Purpose

Measure actual performance characteristics of DAR pattern and compare against traditional approaches:
- Traditional OOP with conditional logic
- Event-driven architecture
- DAR pattern with reactions

## Benchmarks

### 1. ReactionScalingBenchmark ⭐ CRITICAL
**Tests: 1 action with N reactions**
- 1, 5, 10, 20, 50 reactions on same domain
- Single execution and batch (1000 iterations)

**What it measures:**
- How reaction count affects performance (linear? quadratic?)
- Per-reaction overhead cost
- Real-world scaling for complex game entities

### 2. DeepHierarchyReactionsBenchmark ⭐ CRITICAL
**Tests: Reactions propagating up hierarchy**
- Depth 1, 3, 5, 10 (reactions at each level)
- Tests core DAR feature: upward reaction propagation

**What it measures:**
- Cost of tree traversal during reaction invocation
- Hierarchy depth impact on performance
- Real-world nested domain structures

### 3. MultipleActionsBenchmark ⭐ CRITICAL
**Tests: Many action types with reactions**
- 1 action type vs 5 action types
- Each action has its own reaction
- Interleaved execution patterns

**What it measures:**
- Type checking overhead for reactions
- Generic dispatch performance
- Real-world varied gameplay actions

### 4. ComparisonBenchmark
Direct comparison of 3 architectural approaches implementing 5 status effects:
- Traditional OOP: if-statements checking status flags
- Event-driven: Event bus with 5 subscribed handlers
- DAR: 5 independent status domains with reactions

**What it measures:**
- Real-world scenario: status effects modifying attack damage
- 1000 iterations per approach
- Memory allocation patterns

### 5. DARActionExecutionBenchmark
Measures the overhead of DAR's action execution pipeline:
- Action without reactions (baseline)
- Action with 1 After reaction
- Action with 5 reactions (all phases: Prepare, Abort, Before, After x2)

**What it measures:**
- Pipeline overhead per action
- Reaction invocation cost
- Phase-specific costs

### 6. DARHierarchyBenchmark
Measures performance of domain tree hierarchy:
- Flat hierarchy (2 levels)
- Deep hierarchy (5 levels)
- `GetFirst<T>()` traversal performance

**What it measures:**
- Tree traversal overhead
- Domain lookup performance

## Running Benchmarks

```bash
cd Deterministic.GameFramework.Benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release --filter "*ComparisonBenchmark*"

# Run with memory profiler
dotnet run -c Release --memory
```

## Expected Results

Results will show:
- **Mean execution time** per operation
- **Memory allocations** per operation
- **Relative performance** compared to baseline

## Interpreting Results

### Good Performance Indicators:
- DAR overhead < 50ns per action without reactions
- DAR comparable to Event-driven for 5 status effects
- Linear scaling with number of reactions

### Performance Considerations:
- DAR trades minimal runtime overhead for architectural benefits
- Overhead is negligible compared to actual game logic
- Focus should be on maintainability vs raw performance for game logic

## Adding New Benchmarks

1. Create new class in `Benchmarks/` folder
2. Add `[MemoryDiagnoser]` and `[SimpleJob]` attributes
3. Mark methods with `[Benchmark]` attribute
4. Use `[GlobalSetup]` for initialization
5. Mark one benchmark as `[Benchmark(Baseline = true)]`

## Notes

- Benchmarks run in Release mode with optimizations
- Results may vary based on hardware
- Focus on relative performance, not absolute numbers
- Warmup iterations ensure JIT compilation is complete
