# 0043. TimeProvider Integration: Package Split, FakeTimeProvider Injection, and Temporal Strategy Design

**Date:** 2026-04-11
**Status:** Accepted

## Context

Time-dependent logic (caches, tokens, rate limiters, retries, schedulers) is a common source of hard-to-reproduce bugs. .NET 8 introduced `TimeProvider` as a first-class abstraction for time, with `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`) as the test-time implementation. Conjecture can add value by:

1. Generating interesting temporal values (`DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`) with boundary awareness
2. Auto-injecting a `FakeTimeProvider` into property tests when a `TimeProvider` parameter is present
3. Providing DST-aware and epoch-boundary generators for temporal edge cases
4. Providing `Generate.TimeZones()` and `Generate.ClockSet()` for time-zone and distributed-clock scenarios

The design must decide where each concern lives (Core vs. new addon package), how `FakeTimeProvider` is injected without modifying Core, and how deep temporal boundary awareness should go.

## Decision

### 1. Package split: Core for value types, `Conjecture.Time` for clock concerns

Basic value generators (`DateTimeOffset`, `TimeSpan`, `DateOnly`, `TimeOnly`) are added to **`Conjecture.Core`** alongside existing numeric strategies (`Generate.Integers<T>()`, `Generate.Doubles()`). These types have no external dependencies and are common enough to warrant zero-friction access.

Clock injection, `FakeTimeProvider` wrappers, DST-aware extensions, `Generate.TimeZones()`, and `Generate.ClockSet()` are placed in a new **`Conjecture.Time`** addon package. This mirrors the `Conjecture.Formatters` pattern: an optional NuGet package that depends on Core and carries its own external dependencies.

### 2. FakeTimeProvider dependency: reference `Microsoft.Extensions.TimeProvider.Testing`

`Conjecture.Time` takes a direct `<PackageReference>` to `Microsoft.Extensions.TimeProvider.Testing`. This gives users the full official `Advance()` / `SetUtcNow()` API without Conjecture maintaining its own fake. The dependency is isolated to the addon package, never Core.

### 3. Auto-injection via `TimeProviderArbitrary` naming convention

`Conjecture.Time` ships `TimeProviderArbitrary : IStrategyProvider<TimeProvider>`. The existing assembly-scan naming convention in `SharedParameterStrategyResolver.TryGenerateFromArbitraryProvider` discovers `{TypeName}Arbitrary` classes in all loaded assemblies. Adding `Conjecture.Time` to a project automatically enables `TimeProvider` parameter auto-injection — zero changes to Core required.

### 4. DST awareness: simplified heuristics via `TimeZoneInfo.GetAdjustmentRules()`

DST-boundary extensions (`NearDstTransition()`, `NearMidnight()`, `NearLeapYear()`, `NearEpoch()`) use `TimeZoneInfo.GetAdjustmentRules()` to find real system transition dates. No IANA database parser, no external packages. Platform dependency is implicit — the same as `DateTime` already has.

### 5. `ISystemClock` not supported

`ISystemClock` (the older ASP.NET Core abstraction) is obsolete in .NET 8 and removed in .NET 9+. Conjecture targets .NET 10 and will not support deprecated abstractions.

### 6. `StateMachineRunner` auto-advance deferred

`StateMachineRunner.Execute()` will not be changed to auto-advance a shared clock between steps. Users who need clock control in stateful tests embed a `FakeTimeProvider` in `TState` and advance it manually inside `RunCommand()` — this already works today. Auto-advance in the runner is deferred as a future convenience feature.

## Consequences

**Easier:**
- Users testing caches, tokens, and rate limiters get `TimeProvider` auto-injection by just adding the `Conjecture.Time` package
- Core stays lean: no new external dependencies from temporal strategies
- `NearDstTransition()` and `NearLeapYear()` catch real boundary bugs that uniform random generation misses
- `Generate.ClockSet()` enables distributed-system clock-skew tests (natural fit for the future Aspire integration in #62)

**Harder:**
- Users wanting both value generation and clock injection must understand the Core/`Conjecture.Time` split
- DST boundary detection depends on the host platform's time zone database — cross-platform tests may see different transition dates
- `StateMachineRunner` clock integration requires a separate feature cycle when the demand arises

## Alternatives Considered

**All features in `Conjecture.Core`:** Simpler discovery, but forces `Microsoft.Extensions.TimeProvider.Testing` on every Conjecture user. Rejected — the addon pattern already exists for exactly this reason.

**All features in `Conjecture.Time`:** Clean isolation, but means users need an extra `<PackageReference>` just to generate a `TimeSpan`. Rejected — basic temporal types are common enough to belong in Core.

**Own minimal `FakeTimeProvider` implementation:** Avoids the external dependency but creates a maintenance burden and diverges from the official API. Rejected — `Microsoft.Extensions.TimeProvider.Testing` is stable and widely used.

**IANA database for DST:** More accurate leap-second and historical zone data, but adds a significant dependency and maintenance surface. Rejected in favour of `TimeZoneInfo.GetAdjustmentRules()` heuristics, which cover the practical test cases.

**Support `ISystemClock`:** Would help projects on ASP.NET Core 3.x–7. Rejected — those versions are out of support and `ISystemClock` is removed in .NET 9+.
