# Changelog

All notable changes to Conjecture.NET are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [SemVer](https://semver.org/) — API stability guarantees begin at v1.0.0.

---

## [Unreleased]

---

## [0.14.0] — 2026-04-21

### Added

**Core** (`Conjecture.Core`)
- `Generate.For<T>()` / `Generate.For<T>(configure)` — automatic data generation for arbitrary types, with optional per-property strategy overrides via `ForConfiguration<T>`
- `ForConfiguration<T>` — fluent builder for overriding individual property strategies and reading them back by name
- `GenForRegistry` — low-level registry for custom type factories and per-property overrides (`Register`, `RegisterOverride`, `ResolveWithOverrides`)
- `Generate.Guids()` — generates random `Guid` values
- `Generate.Decimals()` / `Generate.Decimals(min, max)` — generates `decimal` values
- `Generate.DateTimes()` / `Generate.DateTimes(min, max)` — generates `DateTime` values
- `Generate.Chars()` — generates random `char` values
- `[GenRange]` attribute — annotate numeric properties with a `[min, max]` range hint for automatic generation
- `[GenStringLength]` attribute — annotate string properties with `minLength`/`maxLength` hints
- `[GenRegex]` attribute — annotate string properties with a regex pattern hint
- `[GenMaxDepth]` attribute — annotate recursive types with a max recursion depth hint

### Changed

**Regex** (`Conjecture.Regex`)
- `Matching`, `NotMatching`, `Email`, `NotEmail`, `Url`, `NotUrl`, `Uuid`, `NotUuid`, `IsoDate`, `NotIsoDate`, `CreditCard`, `NotCreditCard` now surface on `Generate.*` via a C# 14 `extension(Generate)` block on `Conjecture.Core.RegexGenerateExtensions`. A single `using Conjecture.Core;` is enough to see them — no `using Conjecture.Regex;` required. The `RegexGenerate` static factory has been removed (pre-release, no forwarder)
- `KnownRegex` — static class exposing compiled `Regex` instances for common patterns (`Email`, `Url`, `Uuid`, `IsoDate`, `CreditCard`)
- `RegexGenOptions` / `UnicodeCoverage` — options type for controlling Unicode coverage (`Ascii` vs `Full`) in regex-based generators

---

## [0.13.0] — 2026-04-19

### Added

**TestingPlatform** (`Conjecture.TestingPlatform`)
- `ConjectureTestingPlatformExtensions` — extension class for integrating Conjecture with Microsoft Testing Platform (MTP)
- `RegisterConjectureFramework` extension on `ITestApplicationBuilder` — registers the Conjecture test framework with MTP in one call
- `AddExtensions` helper — wires up MTP extensions from command-line args

---

## [0.12.0] — 2026-04-19

### Added

**Core** (`Conjecture.Core`)
- `SelectManyDirectStrategy<TSource, TResult>` — internal zero-alloc `SelectMany` path; accepts `Func<TSource, ConjectureData, TResult>` directly, eliminating the per-`Generate` wrapper allocation (~32 B/call saving)
- Internal `StrategyExtensions.SelectMany` overload routing to `SelectManyDirectStrategy` for hot-path composition

### Changed

**Core** (`Conjecture.Core`)
- `RecursiveStrategy<T>` pre-builds the full depth-level array at construction time, eliminating per-`Generate` `DepthLimitedStrategy` heap allocations
- `WhereStrategy` now rolls back rejected IR nodes (`data.TruncateNodes`) to prevent unbounded node accumulation on filtered-out values

---

## [0.11.0] — 2026-04-18

### Added

**FSharp** (new package `Conjecture.FSharp`)
- `Gen<'a>` type — F#-native generator wrapping `Strategy<T>`
- `Gen` module with primitives: `int`, `bool`, `float`, `string`, `guid`, `byte`, `char`; combinators: `list`, `option`, `result`, `set`, `seq`, `tuple2`
- `Gen.auto<'a>` — automatic generator derivation via `FSharp.Reflection` for records, discriminated unions, and tuples
- `gen { }` computation expression (`GenBuilder`) for monadic generator composition
- `PropertyRunner` — runs property tests from F# using `ConjectureSettings`
- `FSharpFormatter` — pretty-printer for F# union and record values in failure output

**FSharp.Expecto** (new package `Conjecture.FSharp.Expecto`)
- `property` combinator — integrates Conjecture property tests with the Expecto test framework

**Generators** (`Conjecture.Generators`)
- `HierarchyTypeModel` and `HierarchyTypeModelExtractor` — model for sealed class hierarchies
- `HierarchyStrategyEmitter` — emits `Generate.OneOf(…)` strategies for abstract base types with sealed concrete subtypes
- Hierarchy pipeline wired into `ArbitraryGenerator` for auto-generation of sealed hierarchies

**Analyzers** (`Conjecture.Analyzers`)
- CON205 — warns when a concrete subtype of a sealed hierarchy is missing an `[Arbitrary]` attribute

**MCP** (`Conjecture.Mcp`)
- `suggest-strategy-for-sealed-hierarchy` tool — suggests generation strategies for sealed class hierarchies

---

## [0.10.0] — 2026-04-17

### Added

**Core** (`Conjecture.Core`)
- `ConjectureObservability` — static `ActivitySource` and `Meter` singletons (name `"Conjecture.Core"`) for OpenTelemetry trace and metrics integration; zero overhead when no listener is attached
- `PartialConstructorContext` — context carrier for partial constructor generation; exposes `Current` and `Use()` for strategy emitters
- `ConjectureSettings.TestName` — optional test method name; populated by framework adapters to tag the `test.name` attribute on the `PropertyTest` trace span
- `ConjectureSettings.TestClassName` — optional test class name; populated by framework adapters to tag the `test.class.name` attribute on the `PropertyTest` trace span
- OTel Activity spans in `TestRunner`: `PropertyTest` (root), `PropertyTest.Generation`, `PropertyTest.Shrinking`, `PropertyTest.Targeting` — each with relevant tags (seed, examples, reductions, labels, etc.)
- OTel metrics instruments: `conjecture.property.examples_total`, `conjecture.property.failures_total`, `conjecture.property.duration_seconds`, `conjecture.generation.rejections_total`, `conjecture.shrink.passes_total`, `conjecture.shrink.reductions_total`, `conjecture.targeting.best_score`, `conjecture.database.replays_total`, `conjecture.database.saves_total`

**LinqPad** (`Conjecture.LinqPad`)
- `StrategyCustomMemberProvider<T>` — LINQPad custom member provider for `Strategy<T>`; surfaces generated sample values in the LINQPad results panel
- `StrategyLinqPadExtensions.ShrinkTraceHtml<T>` — extension that renders a shrink trace as an HTML object for LINQPad output

---

## [0.9.0] — 2026-04-15

### Added

**TestingPlatform** (new package `Conjecture.TestingPlatform`)
- Native Microsoft Testing Platform adapter — test project runs as a self-contained executable (`OutputType=Exe`), no framework runner required
- `[Property]` attribute with full settings surface: `MaxExamples`, `Seed`, `UseDatabase`, `MaxStrategyRejections`, `DeadlineMs`, `Targeting`, `TargetingProportion`, `ExportReproOnFailure`, `ReproOutputPath`
- CLI options `--conjecture-seed` and `--conjecture-max-examples` to override settings globally at run time
- `ITrxReportCapability` support for TRX report generation via `dotnet test --report-trx`

### Changed

- `Conjecture.Interactive`: all output switched from HTML/SVG to plain text
- `SvgHistogram` renamed to `TextHistogram`
- `ShrinkTraceResult<T>.Html` renamed to `.Text`

### Removed

- `ConjectureKernelExtension` — Polyglot Notebooks auto-load (`Microsoft.DotNet.Interactive` deprecated)
- `Microsoft.DotNet.Interactive` dependency removed from `Conjecture.Interactive`

---

## [0.8.0] — 2026-04-14

### Added

**Analyzers** (new package `Conjecture.Analyzers`, bundled into `Conjecture.Core.nupkg`)
- CON107: Non-deterministic operation inside `[Property]` (`Guid.NewGuid()`, `DateTime.Now`, `Random`, etc.)
- CON108: `Assume.That` condition always true given built-in strategy constraint (`PositiveInts`, `NegativeInts`, `NonNegativeInts`)
- CON109: Missing strategy for `[Property]` parameter type
- CON110: Async `[Property]` method contains no `await`
- CON111: `Target.Maximize`/`Target.Minimize` outside `[Property]` method
- CJ0050: Suggest named extension property (`.Positive`, `.NonEmpty`) instead of equivalent `.Where()` — with code fix

**Time** (new package `Conjecture.Time`)
- `TimeGenerate.TimeZones()` — strategy over system time zones, shrinks toward UTC
- `TimeGenerate.ClockSet(nodeCount, maxSkew)` — generates an array of `FakeTimeProvider` instances with clock skew
- `TimeProviderArbitrary` — `[Arbitrary]` auto-provider for `TimeProvider` parameters
- `DateTimeOffsetExtensions`: `.NearMidnight()`, `.NearLeapYear()`, `.NearEpoch()`, `.NearDstTransition(zone?)`

**Interactive** (new package `Conjecture.Interactive`)
- `Strategy<T>.Preview(count, seed)` — quick-look HTML table of sample values
- `Strategy<T>.SampleTable(count, seed)` — indexed HTML sample table
- `Strategy<T>.Histogram(sampleSize, bucketCount, seed)` — SVG histogram of distribution
- `Strategy<T>.ShrinkTrace(seed, failingProperty)` — step-by-step shrink trace
- `ConjectureKernelExtension` — Polyglot Notebooks auto-load

**Core**
- `IPropertyTest` and `IReproductionExport` interfaces for framework-agnostic attribute introspection
- `ConjectureSettings.From(IPropertyTest, ILogger?)` factory for constructing settings from attribute data
- `ConjectureStrategyRegistrar` for plugging in custom strategy resolution
- `Generate.FromBytes<T>(ReadOnlySpan<byte>)` — deterministic replay from a fixed byte buffer
- `Generate.DateTimeOffsets()` / `Generate.DateTimeOffsets(min, max)`
- `Generate.TimeSpans()` / `Generate.TimeSpans(min, max)`
- `Generate.DateOnlyValues()` / `Generate.DateOnlyValues(min, max)`
- `Generate.TimeOnlyValues()` / `Generate.TimeOnlyValues(min, max)`

**Tool**
- `PlanRunner` resolves `IStrategyProvider<T>` via reflection for arbitrary types in plan steps

---

## [0.7.0] — 2026-04-11

### Added

**Core**
- `DataGen` static class with `Sample<T>`, `SampleOne<T>`, and `Stream<T>` for generating data outside of property tests
- `IOutputFormatter` interface with `Name` and `WriteAsync<T>` for pluggable output serialisation
- `ConjectureSettings.ExportReproOnFailure` and `ReproOutputPath` for saving reproduction scripts on failure
- `StrategyExtensionProperties` extension properties on `Strategy<int>`, `Strategy<string>`, and `Strategy<List<T>>`: `.Positive`, `.Negative`, `.NonZero`, `.NonEmpty`
- `|` operator on `Strategy<T>` via `StrategyExtensionProperties` for strategy union
- `Generate.Identifiers(...)`, `Generate.NumericStrings(...)`, `Generate.VersionStrings(...)` string generators

**Formatters** (new package `Conjecture.Formatters`)
- `JsonOutputFormatter` — serialises generated data as a JSON array
- `JsonLinesOutputFormatter` — serialises generated data as newline-delimited JSON

**Tool** (new package `Conjecture.Tool`)
- `AssemblyLoader` — discovers `IStrategyProvider` types in an assembly
- `GenerateCommand.ExecuteAsync(...)` — CLI entry point for ad-hoc data generation
- `Plan` sub-namespace: `GenerationPlan`, `PlanStep`, `OutputConfig`, `PlanRunner`, `PlanResult`, `RefExpression`, `RefResolver`, `PlanException` for YAML-driven multi-step generation plans

**Xunit**
- `PropertyAttribute.ExportReproOnFailure` and `ReproOutputPath` for per-test repro export configuration

---

## [0.6.0-alpha.1] — 2026-04-05

First public alpha release. All seven implementation phases are complete.

### Added

**Core engine**
- `ConjectureData` byte-stream-backed test case generation
- `Strategy<T>` abstract base with `Draw(ConjectureData)` semantics
- `SplittableRandom` (SplitMix64) reproducible PRNG
- `Assume.That(condition)` for filtering
- `[Property]` attribute with auto-resolved parameter strategies

**Strategy library**
- Primitives: `Generate.Booleans()`, `Generate.Bytes(size)`, `Generate.Integers<T>()`, `Generate.Doubles()`, `Generate.Floats()`
- Strings: `Generate.Strings(...)`, `Generate.Text(...)`
- Collections: `Generate.Lists<T>()`, `Generate.Sets<T>()`, `Generate.Dictionaries<K,V>()`
- Combinators: `Select`, `Where`, `SelectMany`, `Zip`, `OrNull`, `WithLabel`
- Choice: `Generate.Just()`, `Generate.OneOf()`, `Generate.SampledFrom()`, `Generate.Enums<T>()`
- Composition: `Generate.Compose(ctx => ...)` imperative builder
- Recursive: `Generate.Recursive<T>(baseCase, recursive, maxDepth)`
- Stateful: `Generate.StateMachine<TMachine, TState, TCommand>(maxSteps)`

**Shrinking**
- 10-pass byte-stream shrinking (ZeroBlocks, DeleteBlocks, LexMinimize, IntegerReduction, FloatSimplification, StringAware, BlockSwapping, CommandSequence, and more)
- No custom shrinker code required — works universally for all types

**Targeted testing**
- `Target.Maximize(score, label)` / `Target.Minimize(score, label)`
- `IGeneratorContext.Target(score, label)` inside `Generate.Compose`
- Hill-climbing phase after random generation

**Stateful testing**
- `IStateMachine<TState, TCommand>` interface
- Command sequence generation and shrinking
- `StateMachineRun<TState>` result with step-by-step failure reporting

**Test framework adapters**
- `Conjecture.Xunit` — xUnit v2
- `Conjecture.Xunit.V3` — xUnit v3
- `Conjecture.NUnit` — NUnit 4
- `Conjecture.MSTest` — MSTest

**Parameter resolution attributes**
- `[From<TProvider>]` — custom `IStrategyProvider<T>`
- `[FromFactory(methodName)]` — static factory method
- `[Example(args)]` — explicit test cases run before generated ones
- `[Arbitrary]` — source generator marker

**Roslyn tooling** (bundled in `Conjecture.Core`)
- Source generator: auto-derives `IStrategyProvider<T>` for `[Arbitrary]` types
- 6 analyzers: CON100–CON105
- Code fixes for common diagnostics

**Example database**
- SQLite-backed persistence of failing byte buffers
- Automatic replay on subsequent runs for regression prevention

**Structured logging**
- `ILogger` integration via `ConjectureSettings.Logger`
- Auto-wired to framework output in all four adapters
- 12 structured log events covering generation, shrinking, and targeting

**Release infrastructure**
- MinVer tag-based versioning
- SourceLink + deterministic builds
- GitHub Actions release workflow (`v*` tag → NuGet publish)
- Public API tracking (`PublicAPI.Shipped.txt`)

[0.9.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.9.0
[0.8.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.8.0
[0.7.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.7.0
[0.6.0-alpha.1]: https://github.com/kommundsen/Conjecture/releases/tag/v0.6.0-alpha.1
