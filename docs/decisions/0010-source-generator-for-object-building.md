# 0010. Source Generator for Object Building

**Date:** 2026-03-25
**Status:** Accepted

## Context

Hypothesis needs a mechanism to automatically derive `Arbitrary<T>` instances (generators + shrinkers) for user-defined types. Two main approaches exist: runtime reflection (inspect constructors/properties at test time) and compile-time source generation (emit the `Arbitrary<T>` implementation as C# code during build).

## Decision

Use a Roslyn incremental source generator triggered by an `[Arbitrary]` attribute. The generator emits a concrete `Arbitrary<T>` class at compile time, with no runtime reflection.

## Consequences

- NativeAOT and trimming compatibility is preserved — no `Type.GetMembers()` or `Activator.CreateInstance` calls in the hot path.
- Generated code is visible and debuggable (in `obj/` generated sources); users can inspect what the generator produced.
- Compile-time errors surface immediately when a type cannot be automatically handled (e.g., no accessible constructor), rather than failing at test runtime.
- Source generators add build-time complexity and require the `Hypothesis.Generators` package reference; projects not needing auto-derivation can omit it.
- Generator versioning must be managed carefully — generated code is part of the public API surface indirectly.

## Alternatives Considered

- **Runtime reflection**: Simpler to implement initially; no build step required. Breaks NativeAOT, slows test startup, and defers errors to runtime.
- **Manual `Arbitrary<T>` implementations only**: Requires users to write boilerplate for every type. Acceptable as the fallback path but not sufficient as the primary experience.
- **T4 templates**: Older code-generation approach with poor IDE integration and no incremental rebuild support.
