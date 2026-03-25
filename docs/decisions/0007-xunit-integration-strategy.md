# 0007. xUnit Integration Strategy

**Date:** 2026-03-25
**Status:** Accepted

## Context

Conjecture.NET needs to integrate with a .NET test runner so that property-based tests are discovered, executed, and reported like ordinary tests. xUnit v3 is the primary target. Integration can range from thin wrappers (custom `[Fact]`-like attributes that call the engine internally) to deep framework integration via xUnit's extensibility points.

## Decision

Integrate via xUnit v3's first-class extensibility points: `IXunitSerializable` for serializing counter-examples, custom `ITestCase` implementations for parameterised reporting, and `ITestFramework` registration for global engine lifecycle. Surface a `[Property]` attribute as the user-facing entry point.

## Consequences

- Failed property tests report the minimal shrunk counter-example as a named test case, enabling re-run of just the failing case via `dotnet test --filter`.
- Counter-examples are serializable and reproducible across runs without manual seed management.
- The integration is deeper and more complex to implement than a simple wrapper, but results in a significantly better developer experience.
- Ties the primary integration to xUnit v3; NUnit and MSTest integrations are deferred to later phases.

## Alternatives Considered

- **Thin attribute wrapper only**: Simplest to implement; `[Property]` calls the engine inside a `[Fact]`. Counter-examples appear only in exception messages, not as discrete test cases. Lossy UX.
- **NUnit primary target**: NUnit has comparable extensibility but a smaller share of new .NET projects; xUnit v3 is the more active ecosystem.
- **Framework-agnostic runner**: Build a standalone CLI/runner instead. Increases friction for users who expect `dotnet test` to work.
