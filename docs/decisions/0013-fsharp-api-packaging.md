# 0013. F# API Packaging

**Date:** 2026-03-25
**Status:** Accepted

## Context

F# is a first-class .NET language with a strong property-based testing culture (FsCheck). Conjecture.NET's C# API (`Gen<T>`, LINQ combinators) is usable from F# but is not idiomatic — F# users expect a `Gen` module with curried functions and computation expressions, not fluent method chains.

## Decision

Ship a separate `Hypothesis.FSharp` NuGet package that wraps the core engine with an idiomatic F# API: a `Gen` module mirroring FsCheck conventions, a `gen { }` computation expression, and pipe-friendly combinators. The core engine lives in `Hypothesis`; the F# package has a one-way dependency on it.

## Consequences

- F# users get an idiomatic experience without polluting the C# API with F#-specific types (discriminated unions, `FSharpOption<T>`, etc.).
- The core library remains trim-safe and NativeAOT-compatible; the F# wrapper can use F# idioms that may not be trim-safe without affecting the core.
- Two packages to publish, version, and document; the F# package version must track the core package's breaking changes.
- F# users adopting Conjecture.NET contribute to a community separate from FsCheck; care should be taken not to fragment the F# property-testing ecosystem unnecessarily.

## Alternatives Considered

- **Single package with F# extension methods**: Keeps packaging simple but results in a cluttered C# API namespace and non-idiomatic F# experience.
- **F# as primary API**: Would alienate the larger C# user base and complicate interop in the other direction.
- **No F# support**: Leaves a clear gap given F#'s property-testing culture; likely to prompt community forks.
