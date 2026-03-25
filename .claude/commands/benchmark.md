Create or run a BenchmarkDotNet benchmark for Conjecture.NET components.

## Input

$ARGUMENTS — one of:
- `create <component>` — scaffold a new benchmark for a component (e.g., `create IntegerStrategy`)
- `run [filter]` — build and run benchmarks, optionally filtered
- `compare <baseline> <candidate>` — run both and produce a comparison

## Steps

### `create <component>`
1. Ensure `src/Conjecture.Benchmarks/Conjecture.Benchmarks.csproj` exists. If not, create it with BenchmarkDotNet dependency in `Directory.Packages.props`.
2. Create `src/Conjecture.Benchmarks/<Component>Benchmarks.cs`:
   - Use `[MemoryDiagnoser]` and `[SimpleJob]` attributes
   - Benchmark the hot path: generation throughput, shrink iterations, memory allocation
   - Include realistic parameters via `[Params]`
3. Verify it compiles

### `run [filter]`
1. Run `dotnet run -c Release --project src/Conjecture.Benchmarks/ -- --filter "<filter>"`
2. Summarize results: ops/sec, memory, allocations
3. Flag any results that look concerning (high alloc rate, slow throughput)

### `compare <baseline> <candidate>`
1. Run benchmarks for both, outputting to separate result directories
2. Present a side-by-side comparison table
3. Highlight regressions > 10% and improvements > 10%

## Guidelines

- Always run benchmarks in Release configuration
- Focus on: generation throughput (ops/sec), shrink step count, memory per operation
- A strategy generating < 100k values/sec or allocating > 1KB/value is a red flag
