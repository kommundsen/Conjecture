# 0006. Target Framework

**Date:** 2026-03-25
**Status:** Accepted

## Context

Conjecture.NET needs a minimum target framework version. The choice gates which C# language features, runtime APIs, and BCL types are available without polyfills. Key capabilities desired include generic math (`INumber<T>`), generic attributes (C# 11+), `Span<T>`/`Memory<T>` performance primitives, and source generators.

## Decision

Target .NET 10 as the minimum supported runtime.

## Consequences

- C# 14 language features are available throughout the codebase (extension members, field keyword, etc.).
- `INumber<T>` and related generic math interfaces enable a single, boxing-free numeric strategy rather than per-type overloads.
- Generic attributes allow `[Arbitrary<MyType>]` syntax without workarounds.
- `ArrayPool<byte>`, `Span<T>`, and `Memory<T>` are first-class; no compatibility shims needed.
- NativeAOT and trimming support is mature at this target.
- Users on .NET 8/9 LTS cannot use Conjecture.NET without upgrading; this is an intentional trade-off favouring clean APIs over broad compatibility during the 0.x phase.

## Alternatives Considered

- **net8.0 (LTS)**: Wider install base, but lacks C# 14 features and some generic math refinements. Would require workarounds or conditional compilation.
- **net9.0**: Covers most desired features but is not LTS and has a shorter support window than .NET 10.
- **netstandard2.0**: Maximum compatibility but excludes `Span<T>` APIs and modern language features entirely; not viable for this project's goals.
