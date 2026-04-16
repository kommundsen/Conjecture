# 0037. Structured Logging via ILogger + [LoggerMessage] Source Generator

**Date:** 2026-04-05
**Status:** Accepted

## Context

The Conjecture framework has zero observability. Users cannot see assumption rejection rates, shrink progress, or timing during test runs. We need structured logging that integrates with all four test framework adapters (xUnit v2/v3, NUnit, MSTest) with zero user configuration.

## Decision

**ILogger via ConjectureSettings.Logger**
- `ConjectureSettings.Logger` property of type `ILogger`, defaulting to `NullLogger.Instance` from `Microsoft.Extensions.Logging.Abstractions`
- Users who want custom structured logging supply `new ConjectureSettings { Logger = myLogger }`
- Framework adapters auto-wire to their native output mechanism (`ITestOutputHelper`, `TestContext.Out`, `Console.WriteLine`) with no opt-out and no user configuration required

**[LoggerMessage] Source Generator**
- All log methods defined on `internal static partial class Log` using `[LoggerMessage]` attribute
- Compile-time source generation eliminates boxing, runtime template parsing, and allocations
- Event IDs 1–11 across Information, Warning, Error, and Debug levels

**Hot Path Protection**
- No instrumentation in `ConjectureData` draw methods or per-shrink-attempt loops
- Logging only at aggregate phase level: generation complete, shrink complete, targeting complete, failure
- Debug-level per-pass shrink progress guarded by `logger.IsEnabled(LogLevel.Debug)` check

**Auto-wiring Bridge**
- `TestOutputHelperLogger` in Core wraps `Action<string>` delegate — avoids direct test framework dependencies in Core
- Each adapter creates its logger via `TestOutputHelperLogger.FromWriteLine(...)` and sets `settings = settings with { Logger = logger }`
- Always-on; users can suppress by passing `NullLogger.Instance` programmatically

**NativeAOT/Trim Safety**
- `[LoggerMessage]` is source-generated, no reflection at runtime
- `Microsoft.Extensions.Logging.Abstractions` is marked `IsTrimmable`

## Consequences

- `Microsoft.Extensions.Logging.Abstractions` 10.0.0 added to `Conjecture.Core` — lightweight, no transitive dependencies, trim-safe
- `ExampleDatabase` constructor gains an `ILogger` parameter — internal, all 4 adapters + TestRunner must be updated
- `Shrinker.ShrinkAsync` gains an `ILogger` parameter — internal, called only from TestRunner
- `HillClimber.Climb` gains optional `ILogger? logger = null` — preserves existing benchmark call sites
- Only `ConjectureSettings.Logger` is public-facing; all log infrastructure is internal

## Alternatives Considered

- **DI-based ILoggerFactory**: Rejected — Core has no DI container; adds unnecessary complexity
- **EventSource**: Rejected — `ILogger` is the modern standard for .NET libraries; EventSource is lower-level infrastructure
- **ActivitySource/Meter**: Deferred at the time of this ADR — later accepted in ADR-0050 for distributed tracing and aggregate metrics; `ILogger` remains the human-readable diagnostics layer
- **Per-draw/per-shrink instrumentation**: Rejected — these are tight inner loops; instrumentation would meaningfully degrade performance
