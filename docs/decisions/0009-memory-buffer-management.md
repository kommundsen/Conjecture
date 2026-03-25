# 0009. Memory Buffer Management

**Date:** 2026-03-25
**Status:** Accepted

## Context

The Conjecture engine works by drawing bytes from a buffer to generate test values. This buffer is allocated and mutated thousands of times per second during a test run. Naive allocation (e.g., `new byte[]` per draw) creates significant GC pressure, undermining the engine's throughput and making performance benchmarks misleading.

## Decision

Use a zero-allocation buffer model based on `ArrayPool<byte>` for buffer lifecycle management, `Span<T>` for synchronous in-method buffer access, and `Memory<T>` for cases requiring lifetime extension beyond a single stack frame.

## Consequences

- GC pressure during property test runs is minimised; the engine can sustain high iteration counts without triggering Gen 0/1 collections.
- Buffer passing via `Span<T>` is stack-allocated and cannot be stored on the heap, which enforces correct lifetime discipline at compile time.
- Code that must store a slice for later use must explicitly use `Memory<T>` or rent from `ArrayPool<byte>`, making lifetime management explicit and reviewable.
- The API cannot expose raw `Span<T>` across `async` boundaries; care is needed at async entry points.
- Higher implementation complexity than simple `byte[]`; contributors must understand `Span<T>`/`Memory<T>` semantics.

## Alternatives Considered

- **Simple `byte[]` allocations**: Easiest to implement and debug, but incurs GC pressure proportional to the number of draws. Unacceptable for a high-iteration engine.
- **Unsafe fixed buffers**: Maximum performance but eliminates safety guarantees and complicates NativeAOT/trimming support.
- **`MemoryPool<T>`**: Similar to `ArrayPool<byte>` but with a different ownership model (`IMemoryOwner<T>`); adds abstraction overhead without meaningful benefit here.
