# 0016. Settings System Design

**Date:** 2026-03-25
**Status:** Accepted

## Context

Hypothesis tests need configurable parameters: maximum number of examples, deadline per test, suppress health checks, database path, etc. Settings need to be expressible at multiple granularities — globally for a project, per test suite, or per individual test — with inner scopes overriding outer ones.

## Decision

Implement a hierarchical `ConjectureSettings` record with three resolution layers: global defaults → suite-level (via `[assembly: ConjectureSettings(...)]` or a `.hypothesis/settings.json` file) → test-level (via `[Property(MaxExamples = 500)]`). Inner scope wins. Settings are immutable records; derived settings use `with` expressions.

## Consequences

- Immutability eliminates accidental shared-state bugs when settings objects are reused across parallel tests.
- The `with` expression pattern is idiomatic C# 9+ and aligns with the record type model.
- Three-layer resolution covers the common cases (project-wide CI settings, per-suite exploration settings, individual test overrides) without excessive complexity.
- `[assembly:]` attribute-level settings require the attribute to be a valid compile-time constant; complex settings (e.g., custom database path) must use the JSON file instead.
- JSON settings file adds a runtime dependency on `System.Text.Json`, which is already in the BCL for .NET 10.

## Alternatives Considered

- **Single global static settings**: Simple but makes parallel test isolation impossible and test-level overrides awkward.
- **Fluent builder per test**: `Property.WithSettings(s => s.MaxExamples(500))`. Flexible but verbose; no suite-level layer.
- **xUnit fixtures for settings**: Leverages xUnit's existing scope mechanism but couples settings tightly to the test framework, complicating future NUnit/MSTest support.
