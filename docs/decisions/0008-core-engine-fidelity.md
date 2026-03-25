# 0008. Core Engine Fidelity

**Date:** 2026-03-25
**Status:** Accepted

## Context

The heart of Hypothesis is the Conjecture engine: a byte-buffer-driven test-case generator with an integrated shrinker. When porting to .NET, a choice must be made between faithfully translating the Python engine's algorithms and data structures versus reimagining the engine from scratch using .NET idioms or different algorithms.

## Decision

Port the Conjecture engine faithfully from Python, preserving its core algorithms (IR node tree, mutation strategies, shrinker passes) while adapting surface-level idioms to C#.

## Consequences

- Shrinking quality and behaviour will match Python Hypothesis, a well-validated baseline.
- Bug fixes and algorithmic improvements from the Python project can be backported with low translation overhead.
- Python's Hypothesis regression test suite (bug history) can be ported as integration tests, providing a high-confidence correctness signal.
- The engine's internal structure will feel less idiomatic in places — Python's dynamic typing and generator-based coroutines require deliberate C# analogues (`IEnumerable<T>`, `async`/`await`, or explicit state machines).
- Deviating from the Python internals in the future (e.g., for performance) requires explicit divergence tracking.

## Alternatives Considered

- **Reimagine from scratch**: Freedom to use idiomatic C# patterns throughout, but loses the validated shrinking algorithm and the ability to backport Python fixes. High risk of subtle correctness regressions.
- **Wrap Python via interop**: Zero porting effort but introduces a Python runtime dependency, which is unacceptable for a .NET-native library.
