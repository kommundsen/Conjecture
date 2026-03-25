# 0011. Numeric Strategy Design

**Date:** 2026-03-25
**Status:** Accepted

## Context

Conjecture.NET needs built-in strategies for generating numeric types (`int`, `long`, `float`, `double`, `decimal`, `BigInteger`, etc.). The design question is whether to provide per-type overloads (e.g., `Gen.Int()`, `Gen.Long()`) or a single generic strategy parameterised on the numeric type.

## Decision

Provide a single `Gen.Number<T>()` strategy (and range-bounded variants) constrained to `where T : INumber<T>`, using .NET's generic math interfaces introduced in .NET 7 and refined in .NET 10.

## Consequences

- One implementation covers all current and future `INumber<T>` types, including user-defined numeric types.
- No boxing occurs in the hot path — generic math operations are devirtualised by the JIT for value types.
- Range constraints (`Gen.Number<T>(min, max)`) work uniformly across all numeric types without overload explosion.
- Users working with exotic numerics (e.g., `System.Numerics.Complex`, `Half`) get coverage for free.
- Requires .NET 7+ (satisfied by ADR-0006's .NET 10 minimum).
- Slightly less discoverable than named methods (`Gen.Int()`) for users unfamiliar with generic math; XML docs and analyzers can mitigate this.

## Alternatives Considered

- **Per-type overloads (`Gen.Int()`, `Gen.Long()`, etc.)**: Highly discoverable, no generic math knowledge required. Leads to combinatorial explosion when adding range/filter variants, and excludes user-defined numeric types.
- **Hybrid (per-type convenience + generic base)**: Best of both worlds but doubles the API surface and creates documentation burden. Can be added later as convenience wrappers if adoption data supports it.
