# 0057. Conjecture.Money Package Architecture

**Date:** 2026-04-22
**Status:** Accepted

## Context

Property-based tests for financial and monetary logic need to generate realistic currency codes, monetary amounts, and rounding modes. A dedicated `Conjecture.Money` package should follow the established satellite-package conventions (see ADR-0055) without imposing a monetary domain model on the user.

Key constraints:
- Strategies-only: the package must not ship a `Money` or `Currency` record type that callers must adopt
- Must work deterministically across platforms (no runtime locale dependency for currency lists)
- Shrinking must require no custom pass — the existing `NumericAwareShrinkPass` already handles decimal-derived IR nodes
- API surface must be reachable via a single `using Conjecture.Money;` import, consistent with the Regex and Time satellites

## Decision

**Strategies-only package.** `Conjecture.Money` ships strategies for generating currency codes, decimal amounts, and rounding modes. It does not define a `Money` or `Currency` type.

**`Gen.Decimal(min, max, scale)` as an `extension(Generate)` block.** The `DecimalStrategy` and its `Generate.Decimal(...)` factory live in `Conjecture.Money`, exposed through a C# 14 `extension(Generate)` block (identical pattern to `Conjecture.Regex` and `Conjecture.Time`). The extension is placed in the `Conjecture.Core` namespace so callers need only `using Conjecture.Money;`.

**ISO 4217 embedded snapshot.** A static `Iso4217Data` class embeds the ~170 currently active alphabetic currency codes compiled from the official ISO 4217 list. This gives deterministic, cross-platform reproducibility — no dependency on OS locale, `CultureInfo`, or runtime currency tables. The snapshot is version-controlled and updated manually between releases.

**No custom shrinker.** `DecimalStrategy` emits IR nodes that `NumericAwareShrinkPass` already minimises. No additional `IShrinkPass` implementation is needed.

**Allocation helpers are documentation only.** Examples showing how to allocate `MoneyAmount` or similar records belong in docs and samples, not in the production package surface.

**Deferred scope.** NodaMoney interop and crypto amounts (BigInteger/Wei) are out of scope for this package and deferred to separate future packages.

## Consequences

- Callers keep full control of their domain model — no forced `Money` type to wrap or unwrap
- Cross-platform tests produce stable counterexamples regardless of OS locale settings
- The ISO 4217 snapshot requires a manual refresh when new codes are added or removed by the ISO committee (~once every 1–2 years)
- Decimal shrinking is automatic and correct with zero additional code
- The API surface (`Generate.Iso4217Codes()`, `Generate.Amounts(...)`, `Generate.RoundingModes()`, `Generate.Decimal(...)`) is discoverable via a single namespace import

## Alternatives Considered

- **Ship a `CurrencyCode` value type** — rejected; forces a domain type on callers and creates a conversion burden
- **Use `CultureInfo.GetCultures` at runtime** — rejected; non-deterministic across OS versions and unavailable on some .NET targets
- **Custom `DecimalShrinkPass`** — rejected; `NumericAwareShrinkPass` already handles the integer-scaled IR representation that `DecimalStrategy` emits
- **Include NodaMoney support in this package** — rejected; adds a heavyweight dependency for an uncommon use case; better served by a separate `Conjecture.NodaMoney` package
