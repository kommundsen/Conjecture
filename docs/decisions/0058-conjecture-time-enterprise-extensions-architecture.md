# 0058. Conjecture.Time Enterprise Extensions Architecture

**Date:** 2026-04-23
**Status:** Accepted

## Context

`Conjecture.Time` already ships `Generate.TimeZones()`, `Generate.ClockSet()`, boundary-extension methods (`.NearMidnight()`, `.NearLeapYear()`, `.NearEpoch()`, `.NearDstTransition()`), and `TickRangeStrategy<T>`. Issue #292 extends the package with enterprise-grade time strategies covering DST-aware generation, cross-platform IANA zone handling, `DateTimeKind` fuzzing, recurring-event invariants, and DB provider roundtrip helpers.

Several design questions arise:

1. Does adding DST/IANA coverage warrant splitting a `Conjecture.Time.Zones` sub-package?
2. How should DST-biased shrinking interact with the Core shrink engine?
3. Which timezone identifiers are safe across all supported OS platforms?
4. Should recurring-event helpers carry a scheduling dependency (NCrontab, Quartz)?
5. Should NodaTime types be in scope for #292?
6. How should historical timezone data be handled?

## Decision

**1. DST/TZ scope stays in `Conjecture.Time` — no sub-package split.**
The package already owns all timezone and boundary strategy code. Splitting at this stage would add packaging overhead and a second NuGet reference for users who need both halves. The package is not large enough to justify the split.

**2. DST-aware shrinking is strategy-level, not Core-level.**
DST-biased strategies decompose their output into separate IR nodes — e.g. zone index, year offset, hour offset — each drawn as an independent `Generate.Integers<int>` call. The existing `IntegerReductionPass` in Core then reduces each node independently. No Core changes are required, and the shrink behaviour matches the standard numeric reduction semantics already tested and documented.

**3. IANA IDs via a hardcoded cross-platform-safe curated subset (~20 IDs).**
`TimeZoneInfo.GetSystemTimeZones()` returns the full OS zone database but the available IDs differ across Windows (registry-based), Linux (tzdata package), and macOS. A curated subset of ~20 canonical IANA IDs (e.g. `America/New_York`, `Europe/London`, `Asia/Tokyo`, `Australia/Sydney`) is verified to resolve on .NET 8+ on all three platforms. Using this subset over `GetSystemTimeZones()` ensures that `Generate.TimeZones()` generates values that are reproducible across CI environments and developer machines regardless of OS or tzdata version.

**4. Recurring-event helpers are schedule-agnostic delegate-based.**
Recurring event strategies accept `Func<DateTimeOffset, DateTimeOffset?> nextOccurrence` as their scheduling contract. This allows users to plug in any scheduling engine (NCrontab, Quartz, custom cron) without the package carrying those dependencies. `nextOccurrence` returning `null` signals no further occurrences, which terminates generation rather than throwing.

**5. NodaTime types deferred to a future issue.**
#292 is scoped to BCL types (`DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeZoneInfo`, `DateTimeKind`). A NodaTime adapter package is a natural follow-on but introduces a new NuGet dependency and a separate design surface. XML doc comments on the relevant strategies will direct users to the future adapter for NodaTime-specific needs.

**6. Historical timezone data out of scope — use current OS tzdata.**
Generating historically-accurate DST transitions (e.g. the 1883 US railroad-time standardisation) requires IANA tzdata back-files and is an extremely niche use case. The current OS tzdata defines the DST transitions used by `.NearDstTransition()` and the recurring-event strategies. Users who require historical accuracy should use the future NodaTime adapter, which is documented in the XML comments.

## Consequences

- Users of `Conjecture.Time` get DST, IANA, `DateTimeKind`, recurring-event, and DB-roundtrip strategies in one package with one `using` directive — no additional dependency.
- Shrinking of DST-biased values is automatic via existing Core passes; no new shrinker code in either package.
- Cross-platform timezone tests are reproducible across all CI environments because the curated IANA subset is verified on .NET 8+ Windows, Linux, and macOS.
- Recurring-event helpers are maximally flexible (any scheduler) but require the caller to provide a `nextOccurrence` delegate rather than accepting a cron expression directly.
- NodaTime users will need to wait for a future adapter package; the XML comments set this expectation clearly.
- Historical TZ accuracy is not addressable without a future NodaTime or tzdata-backfile integration.

## Alternatives Considered

**`Conjecture.Time.Zones` sub-package split:** Rejected — the package is small, the extra reference burden on users outweighs the separation-of-concerns benefit at current scale.

**Core-level DST shrink pass:** Rejected — embedding calendar-aware shrinking in Core would couple the engine to timezone semantics. Decomposing into independent integer IR nodes achieves the same result without that coupling.

**`GetSystemTimeZones()` for zone sampling:** Rejected — produces non-reproducible test sets across platforms. The curated hardcoded subset is narrower but fully reproducible.

**NCrontab/Quartz dependency for recurring events:** Rejected — adds a transitive dependency to every user of `Conjecture.Time`. A delegate contract is lighter and more composable.

**NodaTime support in #292:** Rejected — out of scope; NodaTime introduces a separate API surface and version matrix. Future issue.
