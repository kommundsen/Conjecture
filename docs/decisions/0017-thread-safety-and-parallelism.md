# 0017. Thread Safety and Parallelism

**Date:** 2026-03-25
**Status:** Accepted

## Context

xUnit v3 runs tests in parallel by default. The Conjecture engine maintains mutable state (current buffer, draw position, shrink pass progress). If engine instances are shared across tests, concurrent access produces data races and non-deterministic results. The design must define the isolation boundary.

## Decision

Each test invocation receives its own engine instance. No engine state is shared between tests. The example database (ADR-0012) is the only shared resource and uses SQLite's connection-level locking for safe concurrent access. Strategies (`Gen<T>` instances) are stateless and freely shareable.

## Consequences

- Tests running in parallel cannot interfere with each other's engine state; no locks are needed on the hot path.
- `Gen<T>` instances are safe to define as `static readonly` fields and share across tests, which is a common usage pattern.
- Per-test engine instantiation is cheap (a few heap allocations); the cost is negligible compared to test execution.
- The database write path must be concurrency-safe; SQLite WAL mode is sufficient for the expected write concurrency.
- If a test inadvertently captures and shares a mutable draw context, bugs will manifest as non-determinism rather than crashes — documentation and analyser warnings should address this.

## Alternatives Considered

- **Thread-local engine instances**: Similar isolation but requires `[ThreadStatic]` or `AsyncLocal<T>`, which complicates async test methods and is harder to reason about.
- **Shared engine with locking**: Serialises all test execution, defeating xUnit's parallel runner. Unacceptable performance trade-off.
- **Immutable engine / functional style**: Fully immutable engine state passed through draws. Elegant but incompatible with the faithful Conjecture port (ADR-0008) and adds significant allocation overhead.
