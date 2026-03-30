# 0030. Framework Adapter Architecture

**Date:** 2026-03-30
**Status:** Accepted

## Context

Phase 2 delivered `Conjecture.Xunit` (v2) as the only framework adapter. Phase 3 adds xUnit v3, NUnit, and MSTest adapters. All four adapters must provide identical `[Property]` semantics: same resolution order for `[From<T>]` / `[FromFactory]` / type-inference, same `[Example]` pre-run behaviour, same async support, same failure reporting, and same database integration. Divergence would create a confusing developer experience and a maintenance burden.

The existing `Conjecture.Xunit` resolver (`ParameterStrategyResolver`) contains the resolution logic inline. Duplicating it into three new adapters is untenable. A shared resolver in `Conjecture.Core` solves this, but raises questions about package boundaries, `internal` visibility, and xUnit v2/v3 coexistence.

## Decision

### SharedParameterStrategyResolver in Core

Extract the resolution logic into `Conjecture.Core.Internal.SharedParameterStrategyResolver`. It is `internal` but shared via `InternalsVisibleTo` entries in `Conjecture.Core.csproj` for all adapter projects:

```
Conjecture.Xunit
Conjecture.Xunit.V3
Conjecture.NUnit
Conjecture.MSTest
```

`SharedParameterStrategyResolver.Resolve(ParameterInfo[], ConjectureData)` implements the resolution order from ADR-0028:

1. `[From<TProvider>]` — instantiate provider, call `Create()`
2. `[FromFactory("name")]` — reflect to static method, invoke
3. `[Arbitrary]` auto-discovery — look for `{TypeName}Arbitrary` in the assembly (Phase 3.9)
4. Type-switch — built-in types (`int`, `string`, `bool`, etc.)
5. `NotSupportedException` with message suggesting `[From<T>]`

Each framework adapter's resolver becomes a thin delegate that calls `SharedParameterStrategyResolver.Resolve(...)`.

### xUnit v2 adapter (Conjecture.Xunit) — preserved, refactored

`Conjecture.Xunit` continues targeting `xunit.extensibility.core` 2.9.x. Its `ParameterStrategyResolver` is refactored to delegate to `SharedParameterStrategyResolver`. No public API change; existing tests remain green. This is Phase 3.16.

### xUnit v3 adapter (Conjecture.Xunit.V3) — new project

xUnit v3 (`xunit.v3.extensibility.core`) has renamed and restructured extensibility types. Key changes from v2:

- Test case discoverer registration uses `[XunitTestCaseDiscoverer]` with a different assembly-qualified type pattern
- `IXunitTestCase`, `XunitTestCaseRunner`, and message bus APIs have breaking changes
- `ITestOutputHelper` integration points differ

`Conjecture.Xunit.V3` is a **separate project** (not a refactor of v2). Users reference exactly one of `Conjecture.Xunit` (v2) or `Conjecture.Xunit.V3` (v3) based on which xUnit version they use. Both packages can coexist in the same solution (different test projects). The package naming convention follows the xUnit team's own `xunit.v3.*` pattern.

Internal structure mirrors v2 but uses v3 extensibility types:

- `PropertyAttribute.cs` — `[XunitTestCaseDiscoverer]` pointing at `PropertyTestCaseDiscoverer`
- `Internal/PropertyTestCaseDiscoverer.cs` — discovers `PropertyTestCase` from `[Property]` methods
- `Internal/PropertyTestCase.cs` — implements v3 `IXunitTestCase`
- `Internal/PropertyTestCaseRunner.cs` — invokes `TestRunner.Run` / `TestRunner.RunAsync`

### NUnit adapter (Conjecture.NUnit) — new project

NUnit's extensibility model uses `ITestBuilder`: an attribute that implements `ITestBuilder` receives the method info and returns `IEnumerable<TestMethod>` describing the test cases. `[Property]` extends `NUnitAttribute` and implements `ITestBuilder`:

```csharp
public sealed class PropertyAttribute : NUnitAttribute, ITestBuilder
{
    public IEnumerable<TestMethod> BuildFrom(IMethodInfo method, Test? suite)
    { ... }
}
```

The built `TestMethod`'s `RunState` is set to `Runnable`. Execution runs `TestRunner.Run`/`RunAsync` with `SharedParameterStrategyResolver` and maps the result to NUnit's pass/fail/inconclusive outcome. Failure messages include the "Falsifying example" text via `CounterexampleFormatter`.

### MSTest adapter (Conjecture.MSTest) — new project

MSTest's extensibility model uses `TestMethodAttribute.Execute(ITestMethod)`: override returns `TestResult[]`. `[Property]` inherits `TestMethodAttribute` and overrides `Execute`:

```csharp
public sealed class PropertyAttribute : TestMethodAttribute
{
    public override TestResult[] Execute(ITestMethod testMethod)
    { ... }
}
```

`Execute` runs `TestRunner.Run`/`RunAsync` with `SharedParameterStrategyResolver`, wraps the result in `TestResult[]`, and places the "Falsifying example" message in `TestResult.TestFailureException.Message`.

### Project targeting

All four adapter projects target `net10.0` (ADR-0006). `Conjecture.Generators` and `Conjecture.Analyzers` target `netstandard2.0` (Roslyn requirement, ADR-0029).

### Attribute reuse from Core

`[From<T>]`, `[FromFactory]`, `[Example]`, `IStrategyProvider<T>`, and `[Arbitrary]` all live in `Conjecture.Core` (ADR-0028, ADR-0029). All four adapters read the same attributes. No adapter defines its own parameter annotation types.

### Identical behaviour contract

All four adapters must satisfy the same behavioural invariants:

- `MaxExamples`, `Seed`, `UseDatabase`, `MaxStrategyRejections`, `DeadlineMs` properties with identical defaults
- `[Example]` cases run before random generation; no shrinking on `[Example]` failure
- Async `Task`-returning properties supported via `TestRunner.RunAsync`
- Failure message contains "Falsifying example", seed, and shrunk counterexample
- Database integration stores and replays counterexamples

Shared end-to-end tests (Phase 3.10–3.12) verify these invariants across all adapters.

## Consequences

- A single `SharedParameterStrategyResolver` change propagates to all four adapters simultaneously, preventing drift.
- `InternalsVisibleTo` is the boundary mechanism — not a public API, so the resolver can remain `internal` and evolve without SemVer constraints.
- xUnit v2/v3 coexistence is clean: separate packages, separate extensibility bases, no shared binary, users pick one.
- NUnit and MSTest adapters follow each framework's idiomatic extensibility pattern, so diagnostics and IDE integrations (Test Explorer, Rider) work out of the box.
- The `net10.0` targeting means adapters cannot be used from `netstandard2.0` test projects, but that is consistent with ADR-0006.
- Adding a 5th framework adapter in a future phase requires only: a new project, `InternalsVisibleTo` entry, and a thin delegation wrapper — the resolver logic is already in Core.

## Alternatives Considered

- **Duplicate resolver per adapter**: Simple initially; leads to four diverging copies within one release cycle. Rejected.
- **Public `ParameterStrategyResolver` in Core**: Would expose engine internals (resolution order, `ConjectureData`) as public API, adding SemVer obligations. Rejected in favour of `internal` + `InternalsVisibleTo`.
- **Monorepo `Conjecture.Adapters.Shared` project**: A fifth project just for the resolver. More indirection for a single class. Rejected; Core is the right home.
- **Merge v2 and v3 into one `Conjecture.Xunit` package**: Would require multi-targeting and runtime version detection with `#if` guards across breaking API changes. Fragile and complicates the package graph. Rejected; separate packages follow the xUnit team's own convention.
- **NUnit via `TestFixtureSource` / parameterized `TestCase`**: NUnit has other extensibility hooks, but `ITestBuilder` is the standard pattern for custom test-generating attributes. Adopted.
- **MSTest via `DataRow` / `DynamicData`**: Pre-supplied data attributes, not execution-time generation. Not applicable.
