# Configuration

Conjecture settings can be configured at three levels, from broadest to most specific.

## 1. Assembly-Level Defaults

Apply to all `[Property]` tests in an assembly:

```csharp
// In your test project's AssemblyInfo.cs or any file:
[assembly: ConjectureSettings(MaxExamples = 500, UseDatabase = false)]
```

## 2. Per-Test via `[Property]` Attribute

Override assembly defaults for a single test:

```csharp
[Property(MaxExamples = 1000, DeadlineMs = 5000)]
public bool Heavy_property(List<int> items) => ...;
```

## 3. Programmatic via `ConjectureSettings` Record

For code that creates settings dynamically:

```csharp
var settings = new ConjectureSettings
{
    MaxExamples = 200,
    Seed = 42UL,
    Deadline = TimeSpan.FromSeconds(5)
};

// Immutable — use `with` to derive:
var ciSettings = settings with { MaxExamples = 1000 };
```

## Settings Reference

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxExamples` | `int` | `100` | Number of generated examples per test. Must be > 0. |
| `Seed` | `ulong?` | `null` | Fixed PRNG seed for reproducibility. `null` = random. On `[Property]`, use `ulong` where `0` = random. |
| `UseDatabase` | `bool` | `true` | Persist and replay failing examples across runs. |
| `DatabasePath` | `string` | `".conjecture/examples/"` | Directory for the example database. |
| `Deadline` | `TimeSpan?` | `null` | Per-example timeout. `null` = no deadline. On `[Property]`, use `DeadlineMs` (int, milliseconds, `0` = no deadline). |
| `MaxStrategyRejections` | `int` | `5` | Max consecutive rejections by a single `Where()` filter before giving up. Must be >= 0. |
| `MaxUnsatisfiedRatio` | `int` | `200` | Max ratio of skipped-to-valid examples before the test is marked as too flaky. Must be >= 0. |
| `Targeting` | `bool` | `true` | Enable or disable the targeted testing phase (hill climbing after generation). See [Targeted Testing](guides/targeted-testing.md). |
| `TargetingProportion` | `double` | `0.5` | Fraction of `MaxExamples` reserved for targeting. Must be in `[0.0, 1.0)`. |
| `Logger` | `ILogger` | `NullLogger.Instance` | Structured logging sink. Adapters auto-wire framework output. See [Observability](guides/observability.md). |

## Reproducibility

Fix the seed to make a test deterministic:

```csharp
[Property(Seed = 12345)]
public bool Deterministic_test(int a, int b) => a + b == b + a;
```

The seed is reported in failure messages, so you can always reproduce a failure by copying it into the attribute.

## Example Database

When `UseDatabase = true`, Conjecture stores failing examples in a local SQLite database at `DatabasePath`. On subsequent runs, these examples are replayed first — ensuring that once a bug is found, it's re-checked every time until the test passes.

Set `UseDatabase = false` in CI if you prefer stateless test runs:

```csharp
[assembly: ConjectureSettings(UseDatabase = false)]
```
