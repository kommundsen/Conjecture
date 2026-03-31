# Phase 3 Implementation Plan: Source Generators, Roslyn Analyzers & Framework Adapters

## Context

Phase 0 delivered the core Conjecture engine (random generation, basic strategies, LINQ combinators, `[Property]` attribute, basic shrinking). Phase 1 extended with rich strategies (floats, strings, collections, choice), formatter pipeline, settings system, and SQLite example database. Phase 2 made it production-quality: 10-pass shrinker (3 tiers), `[Example]`/`[From<T>]`/`[FromFactory]` attributes, async support, enhanced failure reporting, and trim/NativeAOT validation. Phase 3 broadens developer tooling and framework reach: a Roslyn incremental source generator for automatic `Arbitrary<T>` derivation (ADR-0010), a Roslyn analyzer package catching common mistakes at edit time (ADR-0023), and xUnit v3/NUnit/MSTest framework adapters bringing the same `[Property]` experience to all major .NET test frameworks. The existing `Conjecture.Xunit` (v2) adapter is preserved; a new `Conjecture.Xunit.V3` adapter targets xUnit v3's updated extensibility API (matching the xUnit team's own `xunit.v3.*` packaging convention).

**Deferred to Phase 4+:** Stateful testing (ADR-0015), F# API (ADR-0013). Both are independent subsystems that do not affect the core C# developer experience and benefit from Phase 3's source generator (stateful testing needs auto-derived command strategies; F# API needs the stable Gen/Strategy surface Phase 3 validates).

**End-state goal:** A user can write:
```csharp
[Arbitrary]
public partial record Person(string Name, int Age);

[Property]
public void Round_trips([From<PersonArbitrary>] Person p) =>
    Assert.Equal(p, Deserialize(Serialize(p)));
```
and the source generator emits `PersonArbitrary : IStrategyProvider<Person>` at compile time with no reflection. The Roslyn analyzer warns when `min > max` in `Gen.Integers(10, 5)` and suggests `[From<T>]` when `[Arbitrary]` is defined but not used. The same `[Property]` attribute works in NUnit and MSTest projects via `Conjecture.NUnit` and `Conjecture.MSTest` packages.

## Dependency Graph

```
ArbitraryAttribute (Core) ───────────────────────────────┐
                                                          v
               ┌────────────> Conjecture.Generators (source gen) ──┐
               │                                                    │
[Arbitrary] ───┘                                                    │
                                                                    │
CON103 (bounds) ──┐                                                 │
CON100 (assert) ──┤                                                 │
CON104 (assume) ──┼──> Conjecture.Analyzers ────────────────────────┤
CON101 (filter) ──┤                 │                               │
CON102 (async)  ──┘                 │                               │
                                    │                               │
CON105 (arbitrary+from) ───────────┘                               │
                                                                    v
                              ┌──> Conjecture.Xunit.V3 ──> xUnit v3 tests
SharedParameterStrategyResolver ──> Conjecture.NUnit     ──> NUnit tests
                              └──> Conjecture.MSTest     ──> MSTest tests
                                                                    │
Conjecture.Xunit (v2) ──> refactored to use shared resolver        │
                                                                    v
                                                          End-to-end tests
```

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red -> Green -> Refactor -> Verify -> Mark done). 19 sub-phases.

---

### 3.0 Pre-requisites

#### Cycle 3.0.1 -- ADRs
- [x] `/decision` -- ADR-0029: Source Generator Architecture -- incremental generator pipeline, `[Arbitrary]` attribute placement (Core), generated code naming (`{TypeName}Arbitrary`), supported type shapes (records, classes with accessible constructors, structs), error diagnostics, partial class requirement
- [x] `/decision` -- ADR-0030: Framework Adapter Architecture -- adapter project structure, shared resolver extraction, test discovery pattern per framework (xUnit v3, NUnit, MSTest), xUnit v2 vs v3 coexistence strategy

#### Cycle 3.0.2 -- Package references and project scaffolding
- [x] Add `Microsoft.CodeAnalysis.CSharp` to `Directory.Packages.props`
- [x] Add `xunit.v3`, `xunit.v3.extensibility.core`, `NUnit`, `MSTest.TestFramework` to `Directory.Packages.props`
- [x] `/scaffold` -- `Conjecture.Generators` (source generator, `netstandard2.0`)
- [x] `/scaffold` -- `Conjecture.Generators.Tests`
- [x] `/scaffold` -- `Conjecture.Analyzers` (analyzer, `netstandard2.0`)
- [x] `/scaffold` -- `Conjecture.Analyzers.Tests` (with `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`)
- [x] `/scaffold` -- `Conjecture.Xunit.V3` (xUnit v3 adapter, `net10.0`)
- [x] `/scaffold` -- `Conjecture.Xunit.V3.Tests`
- [x] `/scaffold` -- `Conjecture.NUnit` (`net10.0`)
- [x] `/scaffold` -- `Conjecture.NUnit.Tests`
- [x] `/scaffold` -- `Conjecture.MSTest` (`net10.0`)
- [x] `/scaffold` -- `Conjecture.MSTest.Tests`
- [x] `/scaffold` -- `Conjecture.SelfTests` (xUnit v3, dogfooding project)
- [x] Add `InternalsVisibleTo` for `Conjecture.Generators`, `Conjecture.Xunit.V3`, `Conjecture.NUnit`, `Conjecture.MSTest` in `Conjecture.Core.csproj`
- [x] Verify: `dotnet build src/` succeeds with all new empty projects

---

### 3.1 ArbitraryAttribute (Core Foundation)

#### Cycle 3.1.1 -- ArbitraryAttribute
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/ArbitraryAttributeTests.cs`
    - `[Arbitrary]` attribute targets classes/structs/records, `AllowMultiple = false`, marker only (no parameters), can be applied to `partial record`, `partial class`, `partial struct`
  - **Impl** -- `src/Conjecture.Core/ArbitraryAttribute.cs`
    - `[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]`
    - `public sealed class ArbitraryAttribute : Attribute { }`
    - Update `PublicAPI.Unshipped.txt`

---

### 3.2 Source Generator Infrastructure

Generator targets `netstandard2.0` (Roslyn requirement) and ships as development-time-only dependency.

#### Cycle 3.2.1 -- Generator scaffold and empty pipeline
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/GeneratorInfrastructureTests.cs`
    - Generator assembly loads, `ArbitraryGenerator` implements `IIncrementalGenerator`, empty compilation with no `[Arbitrary]` types produces no generated source, no throw on empty compilation
  - **Impl** -- `src/Conjecture.Generators/ArbitraryGenerator.cs`
    - `[Generator(LanguageNames.CSharp)]`, `Initialize(IncrementalGeneratorInitializationContext)` with `SyntaxProvider.ForAttributeWithMetadataName` filtering for `[Arbitrary]`

#### Cycle 3.2.2 -- Type model extraction
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/TypeModelExtractionTests.cs`
    - Record with primary constructor: extracts param names/types
    - Class with single public constructor: extracts param names/types
    - No accessible constructor: produces CON200 diagnostic
    - Non-partial type: produces CON201 diagnostic
    - Generic type: type parameters captured
  - **Impl** -- `src/Conjecture.Generators/TypeModel.cs` + `src/Conjecture.Generators/TypeModelExtractor.cs`
    - `TypeModel` record: `FullyQualifiedName`, `Namespace`, `TypeName`, `TypeKind`, `Members`
    - `MemberModel` record: `Name`, `TypeFullName`, `IsNullable`

---

### 3.3 Source Generator: Strategy Emission

#### Cycle 3.3.1 -- Simple record generation
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/SimpleRecordGeneratorTests.cs`
    - `[Arbitrary] partial record Point(int X, int Y)` generates `PointArbitrary : IStrategyProvider<Point>`
    - Generated `Create()` uses `Strategies.Compose<Point>(...)` with `Gen.Integers<int>()`
    - Generated class is `internal sealed`, file named `Point.g.cs`
    - Compiles without errors, produces valid instances via TestRunner
  - **Impl** -- `src/Conjecture.Generators/StrategyEmitter.cs`
    - Type map: `int` -> `Gen.Integers<int>()`, `string` -> `Gen.Strings()`, `bool` -> `Gen.Booleans()`, `double` -> `Gen.Doubles()`, `float` -> `Gen.Floats()`, `byte` -> `Gen.Integers<byte>()`, `long` -> `Gen.Integers<long>()`
    - Emits `Strategies.Compose<T>(ctx => new T(ctx.Next(s1), ctx.Next(s2), ...))`

#### Cycle 3.3.2 -- Class and struct generation
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/ClassAndStructGeneratorTests.cs`
    - Class with constructor: generates strategy
    - Struct with init properties: generates strategy
    - Multiple constructors: chooses one with most parameters
    - Private constructor skipped
  - **Impl** -- Extend `StrategyEmitter` for class/struct patterns
    - Constructor-based: `new T(ctx.Next(...), ...)`
    - Init-property-based: `new T { Prop1 = ctx.Next(...), ... }`

#### Cycle 3.3.3 -- Nested types and full type mapping
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/NestedTypeGeneratorTests.cs`
    - All supported types map correctly: `string`, `bool`, `int`, `long`, `byte`, `float`, `double`, `List<int>`, enum, `int?`
    - Nested `[Arbitrary]` inside another class: fully qualified naming
    - Member type that is itself `[Arbitrary]`-annotated: references generated provider (`ctx.Next(new AddressArbitrary().Create())`)
    - Unsupported member type: CON202 diagnostic
  - **Impl** -- Extend `StrategyEmitter` with full type map, nested type support, cross-type references

#### Cycle 3.3.4 -- Diagnostic errors for unsupported shapes
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Generators.Tests/GeneratorDiagnosticTests.cs`
    - CON200 (Error): no accessible constructor
    - CON201 (Error): type not partial
    - CON202 (Warning): unsupported member type
    - Valid types produce no diagnostics
  - **Impl** -- `src/Conjecture.Generators/DiagnosticDescriptors.cs`

---

### 3.4 Roslyn Analyzers

#### Cycle 3.4.1 -- CON103: Strategy bounds min > max
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON103Tests.cs`
    - `Gen.Integers(10, 5)` with constant args -> CON103 error
    - `Gen.Integers(0, 100)` -> no diagnostic
    - Non-constant args -> no diagnostic
    - `Gen.Doubles(1.0, 0.5)`, `Gen.Floats(1f, 0f)`, `Gen.Strings(minLength: 10, maxLength: 5)` -> CON103
    - Code-fix: swaps arguments
  - **Impl** -- `src/Conjecture.Analyzers/CON103Analyzer.cs` + `src/Conjecture.Analyzers/CON103CodeFix.cs`

#### Cycle 3.4.2 -- CON100: Assertion inside [Property] method
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON100Tests.cs`
    - `[Property]` with `Assert.Equal(...)` -> CON100 warning
    - `[Property]` with `Assert.True(...)` -> CON100 warning
    - `[Property]` with `x.Should().Be(...)` -> CON100 warning
    - Non-`[Property]` method -> no diagnostic
    - NOTE: Re-evaluate severity -- `Assert.*` is idiomatic in Conjecture's own tests. May scope narrowly (only `void` returns) or downgrade to Info.
  - **Impl** -- `src/Conjecture.Analyzers/CON100Analyzer.cs`

#### Cycle 3.4.3 -- CON104: Assume.That(false)
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON104Tests.cs`
    - `Assume.That(false)` -> CON104 warning
    - `Assume.That(true)` -> no diagnostic
    - `Assume.That(someVar)` -> no diagnostic
  - **Impl** -- `src/Conjecture.Analyzers/CON104Analyzer.cs`

#### Cycle 3.4.4 -- CON102: Sync-over-async in [Property]
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON102Tests.cs`
    - `.GetAwaiter().GetResult()` in `[Property]` -> CON102 info
    - `.Result` on Task in `[Property]` -> CON102 info
    - `.Wait()` on Task in `[Property]` -> CON102 info
    - Same patterns outside `[Property]` -> no diagnostic
    - Code-fix: converts to async + await
  - **Impl** -- `src/Conjecture.Analyzers/CON102Analyzer.cs` + `src/Conjecture.Analyzers/CON102CodeFix.cs`

#### Cycle 3.4.5 -- CON101: High-rejection .Where() predicate
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON101Tests.cs`
    - `Gen.Integers<int>().Where(x => x == 42)` -> CON101 warning
    - `Gen.Booleans().Where(b => b == true)` -> CON101
    - `.Where(x => false)` -> CON101
    - Complex predicates -> no diagnostic (conservative)
  - **Impl** -- `src/Conjecture.Analyzers/CON101Analyzer.cs`
    - Heuristic: equality on unbounded strategies, `false` literals

#### Cycle 3.4.6 -- CON105: [Arbitrary] exists but [From\<T\>] not used
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/CON105Tests.cs`
    - `[Property]` param of type `Person` where `PersonArbitrary` exists -> CON105 info
    - Same with `[From<PersonArbitrary>]` -> no diagnostic
    - Non-`[Property]` method -> no diagnostic
    - Type without `[Arbitrary]` -> no diagnostic
  - **Impl** -- `src/Conjecture.Analyzers/CON105Analyzer.cs`

---

### 3.5 Shared Resolver Extraction

Extract strategy resolution logic into `Conjecture.Core` so all framework adapters share identical resolution.

#### Cycle 3.5.1 -- Extract SharedParameterStrategyResolver to Core
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/SharedParameterStrategyResolverTests.cs`
    - `SharedParameterStrategyResolver.Resolve(parameters, data)` resolves `[From<T>]`, `[FromFactory]`, type-inference fallback
    - Identical behavior to current xUnit resolver
  - **Impl** -- `src/Conjecture.Core/Internal/SharedParameterStrategyResolver.cs`
    - Move logic from `Conjecture.Xunit.Internal.ParameterStrategyResolver`
    - Keep as `internal` (shared via `InternalsVisibleTo`)
    - xUnit resolver becomes thin delegate

---

### 3.6 xUnit v3 Adapter

The existing `Conjecture.Xunit` (v2, `xunit.extensibility.core` 2.9.3) is preserved. `Conjecture.Xunit.V3` is a new project using `xunit.v3.extensibility.core` APIs, matching the xUnit team's `xunit.v3.*` packaging convention. The v3 API has renamed types (e.g., `IXunitTestCase` -> `IXunitTestCase`, `XunitTestCaseRunner` changes, new `ITestOutputHelper`), so this is a separate implementation, not a refactor.

#### Cycle 3.6.1 -- xUnit v3 PropertyAttribute
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/XunitV3PropertyAttributeTests.cs`
    - `[Property]` exists in `Conjecture.Xunit.V3`, has `MaxExamples`, `Seed`, `UseDatabase`, `MaxStrategyRejections`, `DeadlineMs` properties, defaults match v2 variant
  - **Impl** -- `src/Conjecture.Xunit.V3/PropertyAttribute.cs`
    - xUnit v3 extensibility pattern (updated `FactAttribute` base, v3 discoverer registration)
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 3.6.2 -- xUnit v3 test discovery and execution
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/XunitV3PropertyExecutionTests.cs`
    - `[Property]` with `int` param runs/passes, failure reports counterexample, `MaxExamples` respected, seed determinism, `[From<T>]` resolved, `[Example]` runs first, async `Task` works
  - **Impl** -- `src/Conjecture.Xunit.V3/Internal/PropertyTestCaseDiscoverer.cs` + `PropertyTestCase.cs` + `PropertyTestCaseRunner.cs`
    - Uses xUnit v3 extensibility types (`IXunitTestCase`, updated message bus API)
    - Invokes `TestRunner.Run`/`RunAsync` with `SharedParameterStrategyResolver`

#### Cycle 3.6.3 -- xUnit v3 failure reporting and database
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/XunitV3ReportingTests.cs`
    - Failure message has "Falsifying example", seed, shrunk counterexample
    - Database stores/replays counterexamples
    - v3-specific: proper `ITestOutputHelper` integration
  - **Impl** -- Wire `CounterexampleFormatter`, `ExampleDatabase`, `StackTraceTrimmer`

---

### 3.7 NUnit Adapter

#### Cycle 3.7.1 -- NUnit PropertyAttribute
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/NUnitPropertyAttributeTests.cs`
    - `[Property]` exists in `Conjecture.NUnit`, has `MaxExamples`, `Seed`, `UseDatabase`, `MaxStrategyRejections`, `DeadlineMs` properties, defaults match xUnit variant
  - **Impl** -- `src/Conjecture.NUnit/PropertyAttribute.cs`
    - NUnit `ITestBuilder` pattern
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 3.7.2 -- NUnit test execution
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/NUnitPropertyExecutionTests.cs`
    - `[Property]` with `int` param runs/passes, failure reports counterexample, `MaxExamples` respected, seed determinism, `[From<T>]` resolved, `[Example]` runs first, async `Task` works
  - **Impl** -- `src/Conjecture.NUnit/Internal/PropertyTestBuilder.cs`
    - Invokes `TestRunner.Run`/`RunAsync` with `SharedParameterStrategyResolver`

#### Cycle 3.7.3 -- NUnit failure reporting and database
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/NUnitReportingTests.cs`
    - Failure message has "Falsifying example", seed, shrunk counterexample
    - Database stores/replays counterexamples
  - **Impl** -- Wire `CounterexampleFormatter`, `ExampleDatabase`, `StackTraceTrimmer`

---

### 3.8 MSTest Adapter

#### Cycle 3.8.1 -- MSTest PropertyAttribute
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.MSTest.Tests/MSTestPropertyAttributeTests.cs`
    - `[Property]` exists in `Conjecture.MSTest`, inherits `TestMethodAttribute`, has same settings properties
  - **Impl** -- `src/Conjecture.MSTest/PropertyAttribute.cs`
    - Override `TestMethodAttribute.Execute`
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 3.8.2 -- MSTest test execution
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.MSTest.Tests/MSTestPropertyExecutionTests.cs`
    - Same scenarios as NUnit 3.7.2 for MSTest
  - **Impl** -- `src/Conjecture.MSTest/Internal/PropertyTestMethodAttribute.cs`
    - Override `Execute(ITestMethod)` returning `TestResult[]`

#### Cycle 3.8.3 -- MSTest failure reporting and database
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.MSTest.Tests/MSTestReportingTests.cs`
    - Same scenarios as NUnit 3.7.3 for MSTest
  - **Impl** -- Wire formatting/database infrastructure

---

### 3.9 Auto-Discovery of Generated Providers

#### Cycle 3.9.1 -- Wire [Arbitrary] auto-discovery into resolver
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/ArbitraryAutoDiscoveryTests.cs`
    - `[Property]` with param type `Person` (has `[Arbitrary]`) auto-resolves without `[From<PersonArbitrary>]`
    - Explicit `[From<PersonArbitrary>]` takes precedence
    - Type without `[Arbitrary]` falls through to type-inference
    - Generated provider integrates with shrinking
  - **Impl** -- Extend `SharedParameterStrategyResolver`
    - Resolution order: `[From<T>]` -> `[FromFactory]` -> `[Arbitrary]` auto-discovery -> type-switch -> `NotSupportedException`

---

### 3.10 End-to-End: Source Generator

#### Cycle 3.10.1 -- Generator E2E with xUnit v2
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.Tests/EndToEnd/SourceGeneratorE2ETests.cs`
    - `[Arbitrary] partial record` + `[Property]` runs/passes, failing property shrinks, nested `[Arbitrary]` works, auto-discovery works

#### Cycle 3.10.2 -- Generator E2E with xUnit v3
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/EndToEnd/SourceGeneratorXunitV3E2ETests.cs`
    - Same scenarios as 3.10.1 with xUnit v3 `[Property]`

#### Cycle 3.10.3 -- Generator E2E with NUnit
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/EndToEnd/SourceGeneratorNUnitE2ETests.cs`
    - Same scenarios as 3.10.1 with NUnit `[Property]`

#### Cycle 3.10.4 -- Generator E2E with MSTest
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.MSTest.Tests/EndToEnd/SourceGeneratorMSTestE2ETests.cs`
    - Same scenarios as 3.10.1 with MSTest `[Property]`

---

### 3.11 End-to-End: Analyzers

#### Cycle 3.11.1 -- Analyzer integration E2E
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/EndToEnd/AnalyzerIntegrationE2ETests.cs`
    - All 6 diagnostics fire on purpose-built code, code-fixes for CON103/CON102 produce compilable code, no false positives on existing test suite, no interference with source generator

---

### 3.12 End-to-End: Framework Adapters

#### Cycle 3.12.1 -- xUnit v3 adapter E2E
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/EndToEnd/XunitV3AdapterE2ETests.cs`
    - Basic `[Property]`, failing + shrinking, `[Example]` + `[From<T>]` + `[FromFactory]`, async, database, settings

#### Cycle 3.12.2 -- NUnit adapter E2E
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/EndToEnd/NUnitAdapterE2ETests.cs`
    - Same scenarios as 3.12.1

#### Cycle 3.12.3 -- MSTest adapter E2E
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.MSTest.Tests/EndToEnd/MSTestAdapterE2ETests.cs`
    - Same scenarios as 3.12.1

---

### 3.13 Self-Tests (Dogfooding)

`Conjecture.SelfTests` uses Conjecture's own `[Property]` attribute (via `Conjecture.Xunit.V3`) to test itself. This creates a virtuous cycle: regressions in the engine break the self-tests, and improving the engine makes the self-tests more powerful.

#### Cycle 3.13.1 -- Strategy law self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/StrategyLawTests.cs`
    - Functor identity: `strategy.Select(x => x)` produces same distribution as `strategy`
    - Filter true: `strategy.Where(_ => true)` produces same values as `strategy`
    - Filter false: `strategy.Where(_ => false)` always marks invalid
    - SelectMany associativity
    - Collection size bounds respected: `Gen.Lists(s, minSize, maxSize)` always produces within bounds

#### Cycle 3.13.2 -- Shrinker invariant self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/ShrinkerInvariantTests.cs`
    - Shrinking is idempotent: shrinking a fully-shrunk result produces same result
    - Shrinking preserves failure status: shrunk counterexample still triggers original failure
    - Shrinking reduces: shrunk result is lexicographically <= original
    - Bounds respected: shrunk integer values stay within strategy bounds

#### Cycle 3.13.3 -- Source generator self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/GeneratorSelfTests.cs`
    - `[Arbitrary] partial record` used in `[Property]` tests that verify generation properties
    - Generated strategies compose correctly with LINQ combinators
    - Generated nested types resolve cross-references

#### Cycle 3.13.4 -- Database and settings self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/InfrastructureSelfTests.cs`
    - Database round-trips: save then load returns same buffer (as property test)
    - Settings validation: random valid settings always parse correctly
    - Reporting accuracy: shrink count in output matches actual shrink iterations

---

### 3.14 Trim/NativeAOT Validation

#### Cycle 3.14.1 -- Trim validation for generator output
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet publish` with `<PublishTrimmed>true</PublishTrimmed>` produces zero trim warnings from generated code
  - **Impl** -- Ensure generated code uses only trim-safe patterns (`Strategies.Compose<T>`, `Gen.*`)

#### Cycle 3.14.2 -- Analyzer is dev-time only
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Analyzers.Tests/PackagingTests.cs`
    - `PrivateAssets="all"`, no analyzer DLL in published output, targets `netstandard2.0`
  - **Impl** -- Verify `.csproj` packaging

---

### 3.15 API Surface Tracking

#### Cycle 3.15.1 -- PublicAPI tracking for all new projects
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` with no PublicAPI warnings
  - **Impl** -- Update `PublicAPI.Unshipped.txt` for Core (`ArbitraryAttribute`), xUnit v3 (`PropertyAttribute`), NUnit (`PropertyAttribute`), MSTest (`PropertyAttribute`)

---

### 3.16 xUnit v2 Adapter Alignment

Refactor the existing `Conjecture.Xunit` (v2) to delegate to `SharedParameterStrategyResolver`, ensuring identical resolution behavior across all four framework adapters.

#### Cycle 3.16.1 -- Refactor xUnit v2 to use shared resolver
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.Tests/XunitV2SharedResolverTests.cs`
    - Existing xUnit v2 tests still pass after refactor
    - `[From<T>]`, `[FromFactory]`, type inference all work identically
    - `[Arbitrary]` auto-discovery works via shared resolver
  - **Impl** -- Modify `src/Conjecture.Xunit/Internal/ParameterStrategyResolver.cs` to delegate to `SharedParameterStrategyResolver`

---

### 3.17 Performance Baselines

#### Cycle 3.17.1 -- Source generator compilation performance
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Benchmarks/GeneratorBenchmarks.cs`
    - Compilation time with 0/10/50 `[Arbitrary]` types, incremental rebuild, generated strategy throughput vs hand-written
  - **Impl** -- BenchmarkDotNet benchmarks

---

### 3.18 Post-Implementation Verification

#### Cycle 3.18.1 -- Performance baselines
- [ ] `/benchmark` -- Source generator compilation overhead, generated strategy throughput, adapter overhead per framework (xUnit v2/v3, NUnit, MSTest), analyzer analysis time

#### Cycle 3.18.2 -- Full verification
- [ ] `dotnet test src/`
- [ ] `dotnet test src/Conjecture.Xunit.V3.Tests/`
- [ ] `dotnet test src/Conjecture.NUnit.Tests/`
- [ ] `dotnet test src/Conjecture.MSTest.Tests/`
- [ ] `dotnet test src/Conjecture.SelfTests/`
- [ ] `dotnet build src/ -c Release`

## Key Constraints

- **`PublicAPI.Unshipped.txt`** updated every cycle adding public API (ADR-0002, ADR-0003)
- **NativeAOT safe** -- generated code uses `Strategies.Compose<T>()` (trim-safe public API); no reflection in generated output (ADR-0014)
- **Source generator/analyzer target `netstandard2.0`** -- Roslyn requirement; cannot use .NET 10 APIs in generator code itself
- **Analyzer is dev-time only** -- `PrivateAssets="all"` in consuming projects
- **Nullable enabled, warnings as errors** -- null annotations on all public API
- **Internal by default** -- public: `ArbitraryAttribute` (Core), `PropertyAttribute` (xUnit v3, NUnit, MSTest); all generator/analyzer internals remain internal
- **xUnit v2/v3 coexistence** -- `Conjecture.Xunit` (v2, `xunit.extensibility.core` 2.x) and `Conjecture.Xunit.V3` (`xunit.v3.extensibility.core`) are separate packages; users reference exactly one based on their xUnit version
- **File-scoped namespaces** throughout
- **.NET 10** for adapter projects; `netstandard2.0` for generator/analyzer (ADR-0006)
- **No speculative optimization** (ADR-0025)
- **Shared resolver** -- all framework adapters use `SharedParameterStrategyResolver` from Core
- **Diagnostic IDs are stable** -- CON1xx (analyzer), CON2xx (generator) subject to SemVer (ADR-0004)
- **Framework-agnostic attributes** -- `ArbitraryAttribute`, `ExampleAttribute`, `FromAttribute<T>`, `FromFactoryAttribute`, `IStrategyProvider<T>` live in `Conjecture.Core` (ADR-0028)
- **CON100 severity** -- ADR-0023 specifies `Assert.*` in `[Property]` as Warning, but this is idiomatic in Conjecture's own tests. Evaluate during implementation; may narrow scope or downgrade to Info.
- Use `/decision` if design questions arise

## New ADR(s) Needed

- **ADR-0029: Source Generator Architecture** -- incremental pipeline, `[Arbitrary]` in Core, `{TypeName}Arbitrary` naming, supported shapes (records, classes, structs), `Strategies.Compose<T>` emission pattern, CON200/CON201/CON202 diagnostics, cross-type references, `netstandard2.0` targeting
- **ADR-0030: Framework Adapter Architecture** -- `SharedParameterStrategyResolver` in Core, xUnit v3 extensibility pattern, NUnit `ITestBuilder` pattern, MSTest `TestMethodAttribute.Execute` override, xUnit v2/v3 coexistence strategy, attribute reuse from Core, identical behavior contract across all four adapters

## New Project Structure

```
src/
  Conjecture.Core/                    # Existing -- add ArbitraryAttribute, SharedParameterStrategyResolver
  Conjecture.Xunit/                   # Existing (v2) -- refactor to use shared resolver
  Conjecture.Xunit.V3/               # NEW: xUnit v3 adapter (net10.0)
  Conjecture.Xunit.V3.Tests/         # NEW: xUnit v3 integration tests
  Conjecture.Generators/              # NEW: Roslyn source generator (netstandard2.0)
  Conjecture.Generators.Tests/        # NEW: Generator unit tests
  Conjecture.Analyzers/               # NEW: Roslyn analyzer (netstandard2.0)
  Conjecture.Analyzers.Tests/         # NEW: Analyzer unit tests
  Conjecture.NUnit/                   # NEW: NUnit adapter (net10.0)
  Conjecture.NUnit.Tests/             # NEW: NUnit integration tests
  Conjecture.MSTest/                  # NEW: MSTest adapter (net10.0)
  Conjecture.MSTest.Tests/            # NEW: MSTest integration tests
  Conjecture.SelfTests/                # NEW: Dogfooding project (xUnit v3, uses own [Property])
  Conjecture.Tests/                   # Existing
  Conjecture.Xunit.Tests/             # Existing
  Conjecture.Benchmarks/              # Existing
```

## Critical Files

### Modified
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` -- add `ArbitraryAttribute`
- `src/Conjecture.Core/Conjecture.Core.csproj` -- `InternalsVisibleTo` for new projects
- `src/Conjecture.Xunit/Internal/ParameterStrategyResolver.cs` -- delegate to shared resolver
- `src/Directory.Packages.props` -- new package references

### New
- `src/Conjecture.Core/ArbitraryAttribute.cs`
- `src/Conjecture.Core/Internal/SharedParameterStrategyResolver.cs`
- `src/Conjecture.Generators/ArbitraryGenerator.cs`
- `src/Conjecture.Generators/TypeModel.cs`
- `src/Conjecture.Generators/TypeModelExtractor.cs`
- `src/Conjecture.Generators/StrategyEmitter.cs`
- `src/Conjecture.Generators/DiagnosticDescriptors.cs`
- `src/Conjecture.Analyzers/CON100Analyzer.cs`
- `src/Conjecture.Analyzers/CON101Analyzer.cs`
- `src/Conjecture.Analyzers/CON102Analyzer.cs` + `CON102CodeFix.cs`
- `src/Conjecture.Analyzers/CON103Analyzer.cs` + `CON103CodeFix.cs`
- `src/Conjecture.Analyzers/CON104Analyzer.cs`
- `src/Conjecture.Analyzers/CON105Analyzer.cs`
- `src/Conjecture.Xunit.V3/PropertyAttribute.cs`
- `src/Conjecture.Xunit.V3/Internal/PropertyTestCaseDiscoverer.cs`
- `src/Conjecture.Xunit.V3/Internal/PropertyTestCase.cs`
- `src/Conjecture.Xunit.V3/Internal/PropertyTestCaseRunner.cs`
- `src/Conjecture.NUnit/PropertyAttribute.cs`
- `src/Conjecture.NUnit/Internal/PropertyTestBuilder.cs`
- `src/Conjecture.MSTest/PropertyAttribute.cs`
- `src/Conjecture.MSTest/Internal/PropertyTestMethodAttribute.cs`
- `src/Conjecture.SelfTests/StrategyLawTests.cs`
- `src/Conjecture.SelfTests/ShrinkerInvariantTests.cs`
- `src/Conjecture.SelfTests/GeneratorSelfTests.cs`
- `src/Conjecture.SelfTests/InfrastructureSelfTests.cs`

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 3.3 (source generator):
```bash
dotnet test src/ --filter "FullyQualifiedName~Generator"
```

After 3.4 (analyzers):
```bash
dotnet test src/ --filter "FullyQualifiedName~CON"
```

After 3.6-3.8 (framework adapters):
```bash
dotnet test src/Conjecture.Xunit.V3.Tests/
dotnet test src/Conjecture.NUnit.Tests/
dotnet test src/Conjecture.MSTest.Tests/
```

After 3.10-3.12 (E2E):
```bash
dotnet test src/ --filter "FullyQualifiedName~EndToEnd"
```

After 3.13 (self-tests):
```bash
dotnet test src/Conjecture.SelfTests/
```

Final:
```bash
dotnet test src/
dotnet build src/ -c Release
```
