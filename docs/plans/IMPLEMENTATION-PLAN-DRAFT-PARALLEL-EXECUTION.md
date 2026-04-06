# Draft: Parallel Property Execution

## Motivation

Property tests run many examples (default 100, often 1000+). For CPU-bound properties this is fast, but IO-bound properties (database queries, HTTP calls, file operations) spend most of their time waiting. Running examples concurrently would dramatically reduce wall-clock time for these workloads. .NET's mature async infrastructure (`Task`, `Parallel.ForEachAsync`, `Channel<T>`) makes this feasible without sacrificing determinism.

## .NET Advantage

.NET provides first-class concurrency primitives: `Parallel.ForEachAsync` for bounded parallelism, `Channel<T>` for producer-consumer patterns, `SemaphoreSlim` for resource throttling, and `AsyncLocal<T>` (already used by Conjecture for `ConjectureData` context) for per-task ambient state. Combined with .NET 10's JIT improvements for async state machines, parallel execution can be efficient with minimal framework overhead.

## Key Ideas

### Settings Integration
```csharp
[Property(MaxExamples = 1000, MaxParallelism = 8)]
public async Task<bool> DatabaseLookupIsConsistent(int id)
{
    var result = await db.FindAsync(id);
    return result == null || result.Id == id;
}
```

### Execution Model
```
┌─────────────────────────────────────────────┐
│ Property Runner                             │
│                                             │
│  Seed Generator ──► Channel<ConjectureData> │
│                         │                   │
│  ┌──────────┬──────────┬──────────┐         │
│  │ Worker 1 │ Worker 2 │ Worker N │         │
│  │ Example  │ Example  │ Example  │         │
│  └────┬─────┴────┬─────┴────┬─────┘         │
│       │          │          │               │
│       ▼          ▼          ▼               │
│     Results Channel ──► Aggregator          │
└─────────────────────────────────────────────┘
```

- Seed generation remains sequential and deterministic
- Each worker gets an isolated `ConjectureData` instance via `AsyncLocal<T>`
- Results aggregated: first failure triggers shrinking (sequential, on the failing example's buffer)
- Targeted testing observations collected via concurrent dictionary, merged after each batch

### Determinism Guarantees
- Same seed produces same set of examples regardless of parallelism level
- Example ordering is deterministic (generated sequentially, executed in parallel)
- Only the execution is parallel, not the generation — preserves reproducibility
- Shrinking always runs sequentially (mutates a single buffer)

### Failure Handling
- First failure wins: cancel remaining workers, begin shrinking
- Multiple failures: report the first found, note others in diagnostics
- Timeout per example: `ConjectureSettings.Deadline` applies per-worker
- Exception isolation: each worker catches independently, no cross-contamination

### Resource Throttling
```csharp
[Property(MaxParallelism = 4)]  // Limit concurrent DB connections
public async Task<bool> PropWithDbAccess(int x) { ... }

// Or via settings
new ConjectureSettings { MaxParallelism = Environment.ProcessorCount }
```

## Design Decisions to Make

1. Default parallelism: `1` (backward compat, opt-in) or `Environment.ProcessorCount` (aggressive)?
2. How to handle `Target.Maximize/Minimize` in parallel? Observations from concurrent workers need thread-safe aggregation.
3. Should sync (non-async) properties also parallelize? (Less benefit, but possible via `Task.Run`)
4. How to handle test framework thread affinity? (Some frameworks assume single-threaded test execution)
5. Channel-based or `Parallel.ForEachAsync`-based execution? Channel gives more control; `Parallel.ForEachAsync` is simpler.
6. How to report progress during parallel execution? (N of M complete, current failure count)

## Scope Estimate

Medium. Core parallel runner is ~2 cycles. Thread-safe targeted testing and diagnostics add ~1 more.

## Dependencies

- `System.Threading.Channels` (in-box)
- `AsyncLocal<ConjectureData>` (already in use)
- Existing `TestRunner` infrastructure
- `ConjectureSettings` for configuration

## Open Questions

- What's the typical speedup for IO-bound properties? (Need benchmarks with simulated latency)
- How do test framework adapters handle parallel property execution? (xUnit has its own parallelism model — conflicts?)
- Should parallel execution be aware of system resource limits (e.g., max DB connections)?
- How to handle `Assume.That()` rejections in parallel? (Need atomic counter for unsatisfied ratio)
- Is there a meaningful use case for parallel shrinking? (Multiple shrink passes on independent buffer regions)
