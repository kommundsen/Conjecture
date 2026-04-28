---
name: benchmark
description: >
  Create or run a BenchmarkDotNet benchmark for Conjecture components.
  Use this skill whenever the user wants to measure performance, check throughput, compare two implementations, scaffold a benchmark class, or investigate memory allocations — even if they don't say "BenchmarkDotNet" explicitly.
  Triggers on phrases like "benchmark X", "how fast is", "measure performance of", "run benchmarks", "compare baseline vs candidate", or "check allocations".
---

Create or run a BenchmarkDotNet benchmark for Conjecture components.

## Input

One of:
- `create <component>` — scaffold a new benchmark (e.g., `create IntegerStrategy`)
- `run [filter]` — build and run benchmarks, optionally filtered
- `compare <baseline> <candidate>` — run both and produce a comparison

## Steps

### `create <component>`
1. Ensure `src/Conjecture.Benchmarks/Conjecture.Benchmarks.csproj` exists. If not, create it with a BenchmarkDotNet dependency in `Directory.Packages.props`.
2. Create `src/Conjecture.Benchmarks/<Component>Benchmarks.cs`:
   - Apply `[MemoryDiagnoser]` and `[SimpleJob]` attributes
   - Benchmark the hot path: generation throughput, shrink iterations, memory allocation
   - Include realistic parameters via `[Params]`
3. Verify it compiles with `dotnet build src/`.

### `run [filter]`
1. Run `dotnet run -c Release --project src/Conjecture.Benchmarks/ -- --filter "<filter>"` (omit `--filter` to run all).
2. Summarize results: ops/sec, memory, allocations.
3. Flag any concerning results (see Guidelines below).

### `compare <baseline> <candidate>`
1. Run benchmarks for both, outputting to separate result directories.
2. Present a side-by-side comparison table.
3. Highlight regressions > 10% and improvements > 10%.

## Guidelines

- Always run benchmarks in Release configuration — Debug numbers are meaningless.
- Focus on: generation throughput (ops/sec), shrink step count, memory per operation.
- A strategy generating < 100k values/sec or allocating > 1KB/value is a red flag worth flagging to the user.
