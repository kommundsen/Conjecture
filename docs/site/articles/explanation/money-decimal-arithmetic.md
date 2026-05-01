# Why Conjecture.Money uses decimal arithmetic

This page explains three design choices in `Conjecture.Money`: why `decimal` instead of `double`, why an embedded ISO 4217 snapshot instead of runtime discovery, and how shrinking works for money properties.

## Why `decimal` instead of `double` for money testing

`double` is a binary floating-point type. Binary fractions cannot represent most decimal fractions exactly: `0.1 + 0.2` evaluates to `0.30000000000000004` in binary floating-point arithmetic. A property that tests `tax == gross * 0.2m` would fail spuriously if `gross` were a `double` — not because the business logic is wrong, but because the arithmetic model is wrong.

`decimal` is a decimal floating-point type (base 10, 28 significant digits). `0.1m + 0.2m` is exactly `0.3m`. This makes `decimal` the natural choice for money tests:

- Generated amounts have exact decimal representations.
- Arithmetic on amounts produces exact decimal results.
- Shrinking can target exact representable values (e.g. `0.05m` for a 5-cent edge case) rather than the nearest binary approximation.

The practical difference: a counterexample shrunk to `0.10m` is immediately actionable. The same counterexample expressed as `0.1000000000000000055511151231257827021181583404541015625` is not.

## Why an embedded ISO 4217 snapshot

An alternative design would query a live ISO 4217 source — a bundled XML file, a NuGet metadata package, or an HTTP endpoint — at runtime. Each option has a failure mode that is unacceptable in a testing library:

| Source | Failure mode |
|---|---|
| Bundled XML (separate asset) | Breaks when the asset is stripped by publish trimming or embedding is misconfigured |
| NuGet metadata package | Version skew: the metadata package and the strategy package may disagree on the code list |
| HTTP endpoint | Tests fail in offline environments (CI, air-gapped, developer laptop on a plane) |

The embedded snapshot approach means:
- **Determinism.** The same snapshot is used in every test run, on every machine, in every CI environment. A property that was green yesterday is green today even if the ISO committee added or removed a code overnight.
- **No runtime dependencies.** No file I/O, no HTTP, no separate asset file.
- **Fast.** The `FrozenDictionary` is initialised once at application startup and never touched again.

The trade-off is that the snapshot must be updated when ISO 4217 changes. Currency changes are infrequent (a few per decade) and typically involve countries replacing their currency during monetary union events. When an update is needed, the change is a one-line edit to `Iso4217Data.cs` and a new minor version of the package.

## How shrinking works for money properties

`Conjecture.Money` does not implement a custom shrinker. Instead, it relies on the Core engine's `NumericAwareShrinkPass`, which manipulates the **choice-sequence IR** — the sequence of integer draws made during generation.

When `Strategy.Amounts("USD")` generates a value, it internally:

1. Scales the `[0, 10000]` range to integer space: `[0, 1_000_000]` (scale 2 → ×100).
2. Draws a `long` from `Strategy.Integers<long>(0, 1_000_000)`.
3. Divides the drawn integer by `100m` to recover the decimal.

The shrink pass sees the `long` draw and reduces it toward `0` by the standard integer shrink rules. Dividing a smaller integer by `100m` produces a smaller decimal. No money-specific shrinker is needed.

The practical result: a counterexample generated at `9843.27m` will shrink toward `0.00m`, passing through values like `4921.63m`, `2460.81m`, `1230.40m`, … until the smallest failing value is found. The shrunk counterexample is always a valid representable USD amount with exactly 2 decimal places.

## See also

- [Reference: Money strategies](../reference/money-strategies.md)
- [How to test monetary and decimal arithmetic](../how-to/use-money-strategies.md)
- [Explanation: Shrinking](shrinking.md)
