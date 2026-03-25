# 0023. Roslyn Analyzer Package

**Date:** 2026-03-25
**Status:** Accepted

## Context

Users of Conjecture.NET can write property tests that are technically valid C# but represent common mistakes: putting `Assert.*` calls inside a `[Property]` method instead of returning a `bool`, writing `.Where()` filters that reject nearly all values, or specifying integer ranges where `min > max`. These mistakes produce tests that silently pass, run extremely slowly, or throw confusing runtime exceptions. A Roslyn analyzer catches these at edit time in the IDE and fails CI before the problem reaches a reviewer.

## Decision

Ship a separate NuGet package `Hypothesis.Analyzers` containing diagnostics and code-fix providers:

| ID | Severity | Description | Fix |
|----|----------|-------------|-----|
| **CON100** | Warning | Assertion (`Assert.*`, `Should*`, `throw`) inside a `[Property]` method body — the test always passes regardless of the assertion | Suggest returning `bool` or using `Verify.That()` |
| **CON101** | Warning | `.Where()` predicate is statically obviously highly selective (e.g., `x == 42` on an unbounded integer strategy) | Suggest a targeted strategy (e.g., `Just(42)`) |
| **CON102** | Info | `[Property]` method calls `.GetAwaiter().GetResult()` or `.Result` — sync-over-async | Suggest converting to `async [Property]` |
| **CON103** | Error | Strategy bounds are compile-time constants and `min > max` (e.g., `Integers(10, 5)`) — always throws at runtime | Swap bounds or remove the call |
| **CON104** | Warning | `Assume.That(false)` or a statically-false expression — every test case is discarded | Remove or fix the condition |
| **CON105** | Info | A `[Property]` parameter type has a registered `[Arbitrary]` strategy but `[From<T>]` is not used | Suggest adding `[From<T>]` |

Code-fix providers are included for CON100, CON102, and CON103. All diagnostics can be suppressed individually via `#pragma warning disable CONXXX` or `.editorconfig`.

The package is a development-time-only dependency (`PrivateAssets="all"` in the `.csproj`); it ships no runtime assemblies.

## Consequences

- Common misuse patterns are caught at edit time in Rider and Visual Studio, before CI
- The separate package means users can opt out without affecting the main `Hypothesis` package size or build time
- Diagnostic IDs (CON1xx) are part of the public API surface and subject to SemVer (ADR-0004)
- CON101 (high-rejection `.Where`) is necessarily heuristic — static analysis cannot determine runtime rejection rates; false positives are possible for non-obvious predicates
- CON105 requires the analyzer to understand the `[Arbitrary]` source generator's registration model; it must share type-mapping logic with the generator or use well-known naming conventions

## Alternatives Considered

- **Bundle analyzers in the main `Hypothesis` package** — simpler packaging but forces analyzer overhead on all users, including runtime/library targets where analyzers are irrelevant; analyzers are a development-time concern only
- **No analyzers, rely on documentation** — misses the edit-time feedback loop entirely; the most common mistakes (CON100, CON103) are silent failures that can go undetected for a long time
- **Single catch-all diagnostic** — easier to implement but prevents per-rule suppression; individual diagnostic IDs let teams enable or disable specific rules via `.editorconfig` to match their conventions
