# Troubleshooting

## `UnsatisfiedAssumptionException`: filter too restrictive

`Where()` or `Assume.That()` rejected too many values before finding a valid one. The default budget is 5 consecutive rejections per strategy (`MaxStrategyRejections`).

**Fix:** Constrain the input range instead of filtering:

```csharp
// Problematic — most ints aren't prime:
Generate.Integers<int>(2, 1_000_000).Where(IsPrime)

// Better — generate only from a small known set:
Generate.SampledFrom(new[] { 2, 3, 5, 7, 11, 13 })
```

If filtering is unavoidable, raise the budget:

```csharp
[Property(MaxStrategyRejections = 50)]
public bool My_filtered_property(int value) => ...;
```

## `ConjectureException`: health check failure

Thrown when too many examples overall are skipped relative to valid ones. The default ratio is 200:1 (`MaxUnsatisfiedRatio`).

**Fix:** Restructure the strategy to generate only valid inputs, or relax the ratio:

```csharp
[assembly: ConjectureSettings(MaxUnsatisfiedRatio = 500)]
```

## Test is slow

**Causes and fixes:**

- **Too many rejections** — see above; use narrower strategies instead of `Where()`
- **Large search space** — reduce `MaxExamples` during development, increase in CI
- **Expensive property body** — add a per-example deadline to catch regressions:

```csharp
[Property(DeadlineMs = 500)]
public bool Should_be_fast(List<int> items) => ...;
```

- **Enable logging** to see rejection counts:

```csharp
[assembly: ConjectureSettings(/* Logger wired automatically by adapters */)]
```

Adapters auto-wire the test framework's output as the logger. Check the test output for `Skipped` event counts.

## How do I reproduce a failure?

Every failure message includes the seed:

```
Falsifying example after 42 examples (seed: 0xDEADBEEF12345678):
  value = 42
```

Pin it to make the test deterministic:

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool My_property(int value) => ...;
```

Remove the seed once the bug is fixed.

## `[Arbitrary]` type doesn't generate

Check the following:

- The type must be `partial`
- Must have at least one accessible constructor
- All constructor parameter types must be auto-resolvable (primitives, strings, collections, enums, or other `[Arbitrary]` types)

The source generator emits diagnostics when requirements aren't met:

| ID | Description |
|---|---|
| CON200 | No accessible constructor |
| CON201 | Type is not `partial` |
| CON202 | Parameter type has no resolvable strategy |

## Test not discovered by the runner

Check the following:

- Correct adapter package installed for your framework (`Conjecture.Xunit.V3`, `Conjecture.NUnit`, etc.)
- Correct `using` namespace (e.g. `using Conjecture.Xunit.V3;`)
- `[Property]` attribute applied to the method
- Test class is `public`
- MSTest: test class must also have `[TestClass]`
- Method returns `bool`, `void`, `Task`, or `Task<bool>`
