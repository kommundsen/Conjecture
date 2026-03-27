# 0027. IEEE 754 Floating-Point Strategy Design

**Date:** 2026-03-27
**Status:** Accepted

## Context

ADR-0011 established a single generic numeric strategy constrained to `INumber<T>`. The Phase 0 implementation concretised this as `Gen.Integers<T>()` (constrained to `IBinaryInteger<T>`) for integer types. Phase 1 adds floating-point support, which requires a separate strategy because floating-point types have structural properties that integers do not:

- **Special values**: IEEE 754 defines NaN, +∞, -∞, +0, -0, and subnormals. A correct property-based strategy must exercise these, as many bugs only manifest at boundary values.
- **Non-total ordering**: NaN is unequal to itself; range comparisons involving NaN are always false, making naïve rejection-based bounded generation incorrect.
- **Bit-level generation**: Generating floats by reinterpreting raw integer bits is the most faithful way to produce a uniform distribution across the IEEE 754 value space, but it requires a constraint beyond `INumber<T>`.

The `IBinaryFloatingPointIeee754<T>` interface (available since .NET 7, refined in .NET 10) is the correct constraint: it covers `float`, `double`, and `Half`, provides `IsNaN`, `IsInfinity`, `IsFinite`, and is compatible with bit-manipulation via `BitConverter` equivalents.

## Decision

Provide `FloatingPointStrategy<T> where T : struct, IBinaryFloatingPointIeee754<T>, IMinMaxValue<T>` as the implementation type. Expose four public factory methods:

- `Gen.Floats()` — unbounded `float` generation
- `Gen.Doubles()` — unbounded `double` generation
- `Gen.Floats(float min, float max)` — bounded, finite values only
- `Gen.Doubles(double min, double max)` — bounded, finite values only

**Unbounded generation algorithm:**
Draw a full-width integer (32-bit for `float`, 64-bit for `double`) from `ConjectureData` and reinterpret its bits as the target floating-point type via `BitConverter.Int64BitsToDouble` / `BitConverter.Int32BitsToSingle`. This produces a uniform distribution across all representable bit patterns, including NaN, infinities, subnormals, and both zeros. Approximately 5% of draws force-select from a curated "interesting values" list: `NaN`, `+∞`, `-∞`, `+0`, `-0`, `Epsilon` (smallest positive subnormal), `-Epsilon`, `MaxValue`, `MinValue`. This mirrors Hypothesis's `interesting_floats` mechanism and ensures special values are exercised reliably within modest example counts.

**Bounded generation algorithm:**
Require both bounds to be finite (`T.IsFinite(min) && T.IsFinite(max)`); throw `ArgumentException` otherwise. Map the uniform integer draw to the target range using ULP-space linear interpolation: compute the signed integer distance between `min` and `max` in ULP units, draw uniformly in that range, then offset from the `min` bit pattern. This is O(1), unbiased, and never produces NaN or infinity for finite bounds.

**Shrinking:**
The raw integer drawn from `ConjectureData` is what the shrinker reduces. Small integer bit patterns correspond to values near zero in IEEE 754 (subnormals and small normals), so shrinking naturally pulls toward zero without a custom shrinker pass. NaN's `x != x` property does not affect shrink correctness because correctness is determined by whether an exception is thrown, not by value equality.

## Consequences

- `float`, `double`, and `Half` are all covered by one `FloatingPointStrategy<T>` implementation; no duplication.
- Unbounded generation reliably hits NaN and infinities (≈ every 20 draws given the 5% bias).
- Bounded generation never produces non-finite values — matching user expectations for range-constrained tests.
- Shrinking toward zero is automatic; if shrink quality proves inadequate for edge cases, a dedicated float shrinker pass can be added in Phase 2 (extending ADR-0021).
- `decimal` is explicitly out of scope: it does not implement `IBinaryFloatingPointIeee754<T>` and requires a separate strategy if needed.
- The `IMinMaxValue<T>` bound is required for full-range generation; all three supported types (`float`, `double`, `Half`) satisfy it.

## Alternatives Considered

- **`IFloatingPoint<T>` constraint**: Provides `IsNaN`, `IsInfinity`, `IsFinite`, but no bit-manipulation methods. Would require separate concrete implementations for `float` and `double` to access `BitConverter`, defeating the purpose of generics.
- **Per-type concrete strategies (`FloatStrategy`, `DoubleStrategy`)**: Duplicates logic; violates the generic-math pattern established in ADR-0011.
- **`double`-based generation with cast to `float`**: Loses uniform bit-space coverage for `float` and produces incorrect subnormal distribution.
- **Rejection sampling for bounded range**: Generate unbounded and reject out-of-range. Correct but arbitrarily slow for narrow ranges (e.g., `[0.0, 1e-300]`); ULP-based mapping is O(1).
- **`Gen.Number<T>()` extension to cover floats**: `IBinaryInteger<T>` and `IBinaryFloatingPointIeee754<T>` are disjoint hierarchies; a unified entry point would require runtime dispatch or a common base constraint (`INumber<T>`) that lacks the necessary bit-manipulation capabilities for both. Separate factory methods (`Gen.Floats()`, `Gen.Doubles()`) are clearer and retain full type safety.
