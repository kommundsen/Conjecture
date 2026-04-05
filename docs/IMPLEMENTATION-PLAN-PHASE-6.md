# Phase 6 Implementation Plan: Logging

## Context

Phase 0 delivered the core Conjecture engine (random generation, basic strategies, LINQ combinators, `[Property]` attribute, basic shrinking). Phase 1 extended with rich strategies (floats, strings, collections, choice), formatter pipeline, settings system, and SQLite example database. Phase 2 made it production-quality: 10-pass shrinker (3 tiers), `[Example]`/`[From<T>]`/`[FromFactory]` attributes, async support, enhanced failure reporting, and trim/NativeAOT validation. Phase 3 broadened developer tooling: Roslyn source generator for automatic `Arbitrary<T>` derivation, 6 Roslyn analyzers, and xUnit v3/NUnit/MSTest framework adapters. Phase 4 delivered stateful testing: `IStateMachine<TState,TCommand>`, command sequence shrinking, `StateMachineFormatter`. Phase 5 delivered targeted property testing (`Target.Maximize`/`Minimize`, hill climbing, round-robin multi-label) and recursive strategies (`Generate.Recursive<T>`).

The framework currently has zero observability -- users cannot see what the engine is doing during a test run (assumption rejection rates, shrink progress, timing).

Phase 6 adds structured logging via `ILogger` + `[LoggerMessage]` source generator. ActivitySource and Meter are deferred -- they're designed for distributed services, not single-process test runs.

**Deferred to Phase 7+:** F# API (ADR-0013), ActivitySource/Meter.

**End-state goal:** All four adapters auto-wire logging to their native test output mechanism with zero user configuration:
- **xUnit v2/v3**: logs appear in `ITestOutputHelper` output automatically when the test class declares it
- **NUnit**: logs appear via `TestContext.Out`
- **MSTest**: logs appear via `Console.WriteLine` (captured by MSTest's `CaptureTraceOutput=true` default)

Users who want custom structured logging can also supply `new ConjectureSettings { Logger = myLogger }`.

```
[generation complete] valid=100, unsatisfied=23, elapsed=142ms
[shrinking] started: 47 nodes
[shrinking] pass=DeleteBlocks progress=true
[shrinking] pass=IntegerReduction progress=false
[shrinking] complete: 12 nodes, 31 steps, elapsed=18ms
[failure] seed=0xDEADBEEF, examples=101
```

## Dependency Graph

```
ADR-0037 (logging design) ───────────────────────────────────────────┐
                                                                      │
6.0 NuGet dependency (Microsoft.Extensions.Logging.Abstractions) ────┤
         │                                                            │
         v                                                            │
6.1 Log partial class ([LoggerMessage] source-generated methods) ─────┤
         │                                                            │
         v                                                            │
6.2 ConjectureSettings.Logger + TestRunner wiring ───────────────────┤
         │                                                            │
         v                                                            │
6.3 Shrinker + ExampleDatabase + HillClimber ILogger params ─────────┤
         │                                                            │
         v                                                            │
6.4 Framework adapter integration (auto-wire to test output) ─────────┤
         │                                                            │
         v                                                            │
6.5 E2E logging tests ── 6.6 API surface + docs ─────────────────────┘
```

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red -> Green -> Refactor -> Verify -> Mark done). 16 sub-phases.

---

### 6.0 Pre-requisites

#### Cycle 6.0.1 -- ADR-0037: Logging Design
- [x] `/decision` -- ADR-0037: Logging
  - **ILogger** flows via `ConjectureSettings.Logger` (default `NullLogger.Instance` from `Microsoft.Extensions.Logging.Abstractions`)
  - **`[LoggerMessage]` source generator** for compile-time log method generation -- eliminates boxing, runtime template parsing, allocations
  - **Hot path protection**: no instrumentation in `ConjectureData` draw methods or per-shrink-attempt loops; only at aggregate phase level (generation complete, shrink complete, targeting complete, failure)
  - **Auto-wiring**: adapters bridge each framework's native output to `ILogger` via `Action<string>` delegate in Core -- avoids direct test framework dependencies in Core; always-on, no opt-out
  - **NativeAOT/trim-safe**: `[LoggerMessage]` is source-generated, `Microsoft.Extensions.Logging.Abstractions` is `IsTrimmable`
  - Alternatives considered: DI-based `ILoggerFactory` (rejected: Core has no DI), `EventSource` (rejected: `ILogger` is the modern standard), ActivitySource/Meter (deferred: designed for distributed services, not single-process test runs)

#### Cycle 6.0.2 -- NuGet dependency setup
- [x] `/implement-cycle`
  - **Impl**
    - `src/Directory.Packages.props` -- add `<PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />`
    - `src/Conjecture.Core/Conjecture.Core.csproj` -- add `<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />`
  - **Verify** -- `dotnet build src/Conjecture.Core/` succeeds with zero warnings

---

### 6.1 LoggerMessage Source-Generated Methods

#### Cycle 6.1.1 -- Log partial class
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/LogTests.cs`
    - Using a collecting `ILogger`: calling each `Log.*` method emits at the expected level with structured parameters
    - When `IsEnabled` returns false for a level, the method does no work (verified via call count)
  - **Impl** -- `src/Conjecture.Core/Internal/Log.cs`
    - `internal static partial class Log`
    - Information (EventIds 1-6):
      - `GenerationCompleted(ILogger, int valid, int unsatisfied, double durationMs)`
      - `ShrinkingStarted(ILogger, int nodeCount)`
      - `ShrinkingCompleted(ILogger, int nodeCount, int shrinkCount, double durationMs)`
      - `TargetingStarted(ILogger, string labels)`
      - `TargetingCompleted(ILogger, string labels, string bestScores)`
      - `DatabaseReplaying(ILogger, int bufferCount)`
    - Warning (EventIds 7-8):
      - `HighUnsatisfiedRatio(ILogger, int unsatisfied, int valid, int limit)`
      - `DatabaseError(ILogger, string errorMessage, Exception exception)`
    - Error (EventId 9):
      - `PropertyTestFailure(ILogger, int exampleCount, string seed)`
    - Debug (EventIds 10-11):
      - `ShrinkPassProgress(ILogger, string passName, bool madeProgress)`
      - `DatabaseSaved(ILogger, string testIdHash)`

---

### 6.2 Settings + TestRunner Integration

#### Cycle 6.2.1 -- ConjectureSettings.Logger property
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/SettingsLoggerTests.cs`
    - `new ConjectureSettings()` has `Logger` defaulting to `NullLogger.Instance`
    - `new ConjectureSettings { Logger = myLogger }` stores the provided logger
    - `ConjectureSettingsAttribute.Apply` preserves the baseline `Logger` (attribute cannot set ILogger)
  - **Impl**
    - `src/Conjecture.Core/ConjectureSettings.cs` -- add `public ILogger Logger { get; init; } = NullLogger.Instance;`
    - `src/Conjecture.Core/ConjectureSettingsAttribute.cs` -- `Apply` copies `Logger` from baseline settings
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 6.2.2 -- TestRunner logging
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/TestRunnerLoggingTests.cs`
    - Passing run: logger receives `GenerationCompleted` at Information
    - Failing run: logger receives `PropertyTestFailure` at Error with seed
    - High assumption rejection: logger receives `HighUnsatisfiedRatio` at Warning
    - `NullLogger.Instance`: identical behavior, no exceptions
  - **Impl** -- `src/Conjecture.Core/Internal/TestRunner.cs`
    - Extract `settings.Logger` at top of `RunGenerationCore`
    - `Log.GenerationCompleted` after generation loop
    - `Log.PropertyTestFailure` in failure catch
    - `Log.HighUnsatisfiedRatio` warning when ratio exceeds half the limit
    - `Log.TargetingStarted`/`TargetingCompleted` around targeting phase

---

### 6.3 Internal Components

#### Cycle 6.3.1 -- Shrinker logging
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/ShrinkerLoggingTests.cs`
    - `ShrinkingStarted` and `ShrinkingCompleted` at Information with node counts and timing
    - `ShrinkPassProgress` at Debug for each pass (guarded by `IsEnabled`)
    - `NullLogger.Instance`: identical behavior
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker.cs`
    - Add `ILogger logger` parameter to `ShrinkAsync`
    - Log at entry (`ShrinkingStarted`) and exit (`ShrinkingCompleted`)
    - Log per pass inside `logger.IsEnabled(LogLevel.Debug)` guard
    - Update call sites in `TestRunner.cs` to pass `settings.Logger`

#### Cycle 6.3.2 -- ExampleDatabase logging
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/ExampleDatabaseLoggingTests.cs`
    - `Load` emits `DatabaseReplaying` log
    - `Save` emits `DatabaseSaved` log
    - Corrupt database: `DatabaseError` warning
  - **Impl** -- `src/Conjecture.Core/Internal/ExampleDatabase.cs`
    - Add `ILogger logger` parameter to constructor
    - Log on `Load`, `Save`, and errors
    - Update all call sites in adapters and `TestRunner` to pass `logger`

#### Cycle 6.3.3 -- HillClimber logger parameter
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/HillClimberLoggingTests.cs`
    - `HillClimber.Climb` with a collecting logger: receives per-label targeting progress at Debug
    - `NullLogger.Instance`: identical behavior
  - **Impl** -- `src/Conjecture.Core/Internal/HillClimber.cs`
    - Add optional `ILogger? logger = null` parameter
    - Log per-label improvement at Debug (inside `IsEnabled` guard)
    - Update call sites in `TestRunner` to pass `settings.Logger`

---

### 6.4 Framework Adapter Integration

Each adapter auto-wires logging to its native test output mechanism. All adapters also pass `logger` to the `ExampleDatabase` constructor.

| Adapter | Output Mechanism | Bridge |
|---|---|---|
| **xUnit v2** | `ITestOutputHelper` (in `constructorArguments`) | `OfType<ITestOutputHelper>().FirstOrDefault()?.WriteLine` |
| **xUnit v3** | `TestOutputHelper` (created in `Run`) | `outputHelper.WriteLine` |
| **NUnit** | `TestContext.Out` | `context.CurrentContext.Out?.WriteLine` |
| **MSTest** | `Console.WriteLine` | auto-captured by `CaptureTraceOutput=true` |

#### Cycle 6.4.1 -- TestOutputHelperLogger bridge
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/TestOutputHelperLoggerTests.cs`
    - `TestOutputHelperLogger` implements `ILogger`
    - `IsEnabled` returns true for `Information` and above by default
    - `Log` formats `[level] message` and calls `writeLine`
    - `BeginScope` returns no-op disposable
    - `FromWriteLine(null)` returns `NullLogger.Instance`
  - **Impl** -- `src/Conjecture.Core/Internal/TestOutputHelperLogger.cs`
    - `internal sealed class TestOutputHelperLogger(Action<string> writeLine, LogLevel minLevel = LogLevel.Information) : ILogger`
    - Uses `Action<string>` not `ITestOutputHelper` directly -- avoids xUnit dependency in Core
    - `internal static ILogger FromWriteLine(Action<string>? writeLine, LogLevel minLevel = LogLevel.Information)`

#### Cycle 6.4.2 -- xUnit v2 auto-wires ITestOutputHelper
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.Tests/PropertyAttributeLoggingTests.cs`
    - Test class with `ITestOutputHelper` ctor: Conjecture logs appear in test output
    - Test class without `ITestOutputHelper`: no exceptions, NullLogger used
  - **Impl** -- `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs`
    - Scan `ConstructorArguments` for `ITestOutputHelper`, create logger via `FromWriteLine`
    - `settings = settings with { Logger = logger };`
    - `new ExampleDatabase(dbPath, settings.Logger)`

#### Cycle 6.4.3 -- xUnit v3 auto-wires ITestOutputHelper
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.V3.Tests/PropertyAttributeLoggingTests.cs`
    - Test class with `ITestOutputHelper` ctor: Conjecture logs appear in test output
    - Test class without `ITestOutputHelper`: no exceptions, NullLogger used
  - **Impl** -- `src/Conjecture.Xunit.V3/Internal/PropertyTestCase.cs`
    - After creating `outputHelper`, `ILogger logger = TestOutputHelperLogger.FromWriteLine(outputHelper.WriteLine)`
    - Pass to settings and ExampleDatabase

#### Cycle 6.4.4 -- NUnit auto-wires TestContext.Out
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.NUnit.Tests/PropertyAttributeLoggingTests.cs`
    - Conjecture log output appears in NUnit test output
  - **Impl** -- `src/Conjecture.NUnit/Internal/PropertyTestCommand.cs`
    - `ILogger logger = TestOutputHelperLogger.FromWriteLine(msg => context.CurrentContext.Out?.WriteLine(msg))`
    - Pass to settings and ExampleDatabase

#### Cycle 6.4.5 -- MSTest auto-wires Console.WriteLine
- [ ] `/implement-cycle`
  - **Tests** -- All adapter test suites pass
  - **Impl** -- `src/Conjecture.MSTest/PropertyAttribute.cs`
    - `ILogger logger = TestOutputHelperLogger.FromWriteLine(Console.WriteLine)`
    - Pass to settings and ExampleDatabase
    - No extra NuGet -- `Microsoft.Extensions.Logging.Abstractions` flows transitively from Core

---

### 6.5 End-to-End Logging Tests

#### Cycle 6.5.1 -- Logging end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Logging/EndToEnd/LoggingEndToEndTests.cs`
    - Passing test: `GenerationCompleted` at Information
    - Failing test: `PropertyTestFailure` at Error + `ShrinkingStarted`/`ShrinkingCompleted`
    - Targeting test: `TargetingStarted`/`TargetingCompleted` at Information
    - Assumption rejection: `HighUnsatisfiedRatio` at Warning
    - `NullLogger.Instance`: zero entries, no exceptions

---

### 6.6 API Surface + Documentation

#### Cycle 6.6.1 -- PublicAPI.Unshipped.txt update
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` with zero `RS0016`/`RS0017` warnings
  - **Impl** -- `src/Conjecture.Core/PublicAPI.Unshipped.txt`
    - Add `ConjectureSettings.Logger` get + init

#### Cycle 6.6.2 -- ADR-0037 document
- [ ] `/implement-cycle`
  - **Impl** -- `docs/decisions/0037-observability.md`

#### Cycle 6.6.3 -- DocFX logging guide
- [ ] `/implement-cycle`
  - **Impl** -- `docs/site/articles/guides/observability.md`
    - Enabling custom logging: `new ConjectureSettings { Logger = loggerFactory.CreateLogger("Conjecture") }`
    - Log event catalog: EventIds, levels, message templates
    - Per-adapter DocFX tab groups showing auto-wired output for each framework
    - Performance notes (NullLogger, `[LoggerMessage]` source generation)
    - Update `docs/site/articles/guides/toc.yml`

#### Cycle 6.6.4 -- XML doc audit
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces zero CS1591 warnings
  - **Impl** -- XML doc on `ConjectureSettings.Logger`

---

## Key Constraints

- **`ConjectureSettings` is a record** -- `Logger` is `init`-only, same pattern as existing properties. Settings constructed once per test run.
- **`ExampleDatabase` constructor change** -- internal, all 4 adapters + TestRunner must pass `logger`. This is the main call-site ripple.
- **`Shrinker.ShrinkAsync` signature change** -- internal, called only from TestRunner.
- **`HillClimber.Climb`** -- optional `ILogger? logger = null` preserves existing call sites (benchmarks).
- **`[LoggerMessage]` requires `Microsoft.Extensions.Logging.Abstractions` >= 8.0** -- using 10.0.0 (consistent with other `Microsoft.Extensions.*` packages).
- **No per-draw instrumentation** -- `ConjectureData` draw methods are tight loops, off-limits.
- **No per-shrink-attempt instrumentation** -- only log at pass level, inside `IsEnabled(Debug)` guard.
- **Auto-wiring is always-on** -- no opt-out via `[Property]` attribute. Users who want to suppress can pass `NullLogger.Instance` via `ConjectureSettings` on the programmatic API.
- **NativeAOT/trim-safe** -- `[LoggerMessage]` source-generates methods, no reflection.
- **`PublicAPI.Unshipped.txt`** -- only `ConjectureSettings.Logger` is public.
- **File-scoped namespaces, `sealed` on non-inheritance, nullable enabled, camelCase private fields.**
- Use `/decision` if design questions arise.

## New ADRs Needed

- **ADR-0037: Logging** -- ILogger via ConjectureSettings, [LoggerMessage] source generator, hot path protection, adapter auto-wiring strategy

## New Project Structure

No new projects. All implementation in existing projects:

```
src/
  Conjecture.Core/                    # Existing -- add logging infrastructure
  │                                   # Modify: ConjectureSettings.cs,
  │                                   #   ConjectureSettingsAttribute.cs,
  │                                   #   Conjecture.Core.csproj,
  │                                   #   PublicAPI.Unshipped.txt
  │   Internal/
  │     Log.cs                       # NEW -- [LoggerMessage] source-generated methods
  │     TestOutputHelperLogger.cs    # NEW -- Action<string> -> ILogger bridge
  │     TestRunner.cs                # MODIFY (add logging)
  │     Shrinker.cs                  # MODIFY (add ILogger param)
  │     ExampleDatabase.cs           # MODIFY (add ILogger ctor param)
  │     HillClimber.cs               # MODIFY (add optional ILogger param)
  Conjecture.Tests/
  │   Internal/
  │     LogTests.cs                  # NEW
  │     SettingsLoggerTests.cs       # NEW
  │     TestRunnerLoggingTests.cs    # NEW
  │     ShrinkerLoggingTests.cs      # NEW
  │     ExampleDatabaseLoggingTests.cs  # NEW
  │     HillClimberLoggingTests.cs   # NEW
  │     TestOutputHelperLoggerTests.cs  # NEW
  │   Logging/
  │     EndToEnd/
  │       LoggingEndToEndTests.cs    # NEW
  Conjecture.Xunit/                   # MODIFY PropertyTestCaseRunner.cs
  Conjecture.Xunit.Tests/            # NEW PropertyAttributeLoggingTests.cs
  Conjecture.Xunit.V3/               # MODIFY PropertyTestCase.cs
  Conjecture.Xunit.V3.Tests/         # NEW PropertyAttributeLoggingTests.cs
  Conjecture.NUnit/                   # MODIFY PropertyTestCommand.cs
  Conjecture.NUnit.Tests/            # NEW PropertyAttributeLoggingTests.cs
  Conjecture.MSTest/                  # MODIFY PropertyAttribute.cs
  Directory.Packages.props           # MODIFY (add logging abstractions)
docs/
  decisions/
    0037-observability.md            # NEW
  site/articles/guides/
    observability.md                 # NEW
    toc.yml                          # MODIFY (add observability entry)
```

## Critical Files

### Modified
- `src/Directory.Packages.props` -- add logging abstractions
- `src/Conjecture.Core/Conjecture.Core.csproj` -- add package reference
- `src/Conjecture.Core/ConjectureSettings.cs` -- add `Logger` property
- `src/Conjecture.Core/ConjectureSettingsAttribute.cs` -- preserve Logger in Apply
- `src/Conjecture.Core/Internal/TestRunner.cs` -- logging throughout
- `src/Conjecture.Core/Internal/Shrinker.cs` -- add `ILogger logger` param
- `src/Conjecture.Core/Internal/HillClimber.cs` -- add optional `ILogger? logger = null` param
- `src/Conjecture.Core/Internal/ExampleDatabase.cs` -- add `ILogger logger` ctor param
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` -- add Logger API
- `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs` -- auto-wire, pass Logger to ExampleDatabase
- `src/Conjecture.Xunit.V3/Internal/PropertyTestCase.cs` -- auto-wire, pass Logger to ExampleDatabase
- `src/Conjecture.NUnit/Internal/PropertyTestCommand.cs` -- auto-wire, pass Logger to ExampleDatabase
- `src/Conjecture.MSTest/PropertyAttribute.cs` -- auto-wire, pass Logger to ExampleDatabase
- `docs/site/articles/guides/toc.yml` -- add observability entry

### New
- `src/Conjecture.Core/Internal/Log.cs` -- [LoggerMessage] source-generated methods
- `src/Conjecture.Core/Internal/TestOutputHelperLogger.cs` -- `Action<string>` -> `ILogger` bridge
- `docs/decisions/0037-observability.md`
- `docs/site/articles/guides/observability.md`
- All test files per cycle above

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 6.1: `dotnet test src/ --filter "FullyQualifiedName~LogTests"`
After 6.2: `dotnet test src/ --filter "FullyQualifiedName~SettingsLogger"`
After 6.2: `dotnet test src/ --filter "FullyQualifiedName~TestRunnerLogging"`
After 6.3: `dotnet test src/ --filter "FullyQualifiedName~Shrinker"` and `~Database` and `~HillClimber`
After 6.4:
```bash
dotnet test src/Conjecture.Xunit.Tests/
dotnet test src/Conjecture.Xunit.V3.Tests/
dotnet test src/Conjecture.NUnit.Tests/
dotnet test src/Conjecture.MSTest.Tests/
```
After 6.5: `dotnet test src/ --filter "FullyQualifiedName~LoggingEndToEnd"`
After 6.6: `dotnet build src/ -c Release`

Final:
```bash
dotnet test src/
dotnet test src/Conjecture.SelfTests/
dotnet test src/Conjecture.Xunit.V3.Tests/
dotnet test src/Conjecture.NUnit.Tests/
dotnet test src/Conjecture.MSTest.Tests/
dotnet build src/ -c Release
```
