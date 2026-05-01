# How to troubleshoot common problems

## `UnsatisfiedAssumptionException` — filter too restrictive

`Where()` or `Assume.That()` rejected too many consecutive values. The default budget is 5 consecutive rejections per strategy (`MaxStrategyRejections`).

**Fix:** Constrain the input range instead of filtering:

```csharp
// Problematic — most ints aren't prime:
Strategy.Integers<int>(2, 1_000_000).Where(IsPrime)

// Better — generate only from a small known set:
Strategy.SampledFrom(new[] { 2, 3, 5, 7, 11, 13 })
```

If filtering is unavoidable, raise the budget:

```csharp
[Property(MaxStrategyRejections = 50)]
public bool My_filtered_property(int value) => ...;
```

## `ConjectureException` — health check failure

Thrown when too many examples overall are skipped relative to valid ones. The default ratio is 200:1 (`MaxUnsatisfiedRatio`).

**Fix:** Restructure the strategy to generate only valid inputs, or relax the ratio:

```csharp
[assembly: ConjectureSettings(MaxUnsatisfiedRatio = 500)]
```

## Test is slow

**Too many rejections** — use narrower strategies instead of `Where()`.

**Too many examples** — reduce `MaxExamples` during development, increase in CI.

**Expensive property body** — add a per-example deadline to catch regressions:

```csharp
[Property(DeadlineMs = 500)]
public bool Should_be_fast(List<int> items) => ...;
```

**Enable logging** to see rejection counts — adapters auto-wire test output. Check for `unsatisfied` counts in the generation summary.

## How do I reproduce a failure?

See [How to reproduce a failure](reproduce-a-failure.md).

## `[Arbitrary]` type doesn't generate

Check:

- The type must be `partial`
- Must have at least one accessible constructor
- All constructor parameter types must be auto-resolvable (primitives, strings, collections, enums, or other `[Arbitrary]` types)

The source generator reports diagnostics when requirements aren't met:

| ID | Description |
|---|---|
| CON200 | No accessible constructor |
| CON201 | Type is not `partial` |
| CON202 | Parameter type has no resolvable strategy |

See [How to use source generators](use-source-generators.md) for full requirements.

## Test not discovered by the runner

Check:

- Correct adapter package installed (`Conjecture.Xunit.V3`, `Conjecture.NUnit`, etc.)
- Correct `using` namespace (`using Conjecture.Xunit.V3;`)
- `[Property]` attribute applied to the method
- Test class is `public`
- MSTest: test class must also have `[TestClass]`
- Method returns `bool`, `void`, `Task`, or `Task<bool>`
