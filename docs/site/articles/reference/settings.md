# Settings reference

`ConjectureSettings` controls engine behavior per-test or across an assembly.

## Settings precedence

Settings are resolved in this order (most specific wins):

1. `[Property]` attribute — per-test overrides
2. `[assembly: ConjectureSettings]` — assembly-level defaults
3. Built-in defaults — `ConjectureSettings` record defaults

## Complete settings table

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxExamples` | `int` | `100` | Number of generated examples per test. Must be > 0. |
| `Seed` | `ulong?` | `null` | Fixed PRNG seed for reproducibility. `null` = random. On `[Property]`, use `ulong` where `0` = random. |
| `Database` | `bool` | `true` | Persist and replay failing examples across runs. |
| `DatabasePath` | `string` | `".conjecture/examples/"` | Directory for the example database. |
| `Deadline` | `TimeSpan?` | `null` | Per-example timeout. `null` = no deadline. On `[Property]`, use `DeadlineMs` (int milliseconds; `0` = no deadline). |
| `MaxStrategyRejections` | `int` | `5` | Max consecutive rejections by a single `Where()` filter before throwing. Must be ≥ 0. |
| `MaxUnsatisfiedRatio` | `int` | `200` | Max ratio of skipped-to-valid examples before the test is marked as too flaky. Must be ≥ 0. |
| `Targeting` | `bool` | `true` | Enable or disable the targeted testing phase (hill climbing after generation). |
| `TargetingProportion` | `double` | `0.5` | Fraction of `MaxExamples` reserved for targeting. Must be in `[0.0, 1.0)`. |
| `Logger` | `ILogger` | `NullLogger.Instance` | Structured logging sink. Adapters auto-wire framework output. |
| `ExportReproductionOnFailure` | `bool` | `false` | Write the shrunk counterexample byte buffer to a file on failure. |
| `ReproductionOutputPath` | `string` | `".conjecture/repros/"` | Directory for exported repro files. Used when `ExportReproductionOnFailure = true`. |
| `TestName` | `string?` | `null` | Test method name. Populated automatically by framework adapters; populates the `test.name` tag on the `PropertyTest` trace span. |
| `TestClassName` | `string?` | `null` | Test class name. Populated automatically by framework adapters; populates the `test.class.name` tag on the `PropertyTest` trace span. |

## `[Property]` attribute properties

These properties are available on the `[Property]` attribute (per-test only):

| Property | Type | Default | Notes |
|---|---|---|---|
| `MaxExamples` | `int` | `100` | |
| `Seed` | `ulong` | `0` | `0` = random seed |
| `Database` | `bool` | `true` | |
| `MaxStrategyRejections` | `int` | `5` | |
| `DeadlineMs` | `int` | `0` | `0` = no deadline |
| `Targeting` | `bool` | `true` | |
| `TargetingProportion` | `double` | `0.5` | |
| `ExportReproductionOnFailure` | `bool` | `false` | |
| `ReproductionOutputPath` | `string` | `".conjecture/repros/"` | |

## Assembly-level defaults

Apply settings to all tests in an assembly:

```csharp
// In any .cs file in your test project:
using Conjecture.Core;

[assembly: ConjectureSettings(MaxExamples = 500, Database = false)]
```

## Programmatic settings

The `ConjectureSettings` record is immutable and supports `with` expressions:

```csharp
ConjectureSettings defaults = new();
ConjectureSettings ci = defaults with { MaxExamples = 1000, Database = false };
ConjectureSettings debug = defaults with { Seed = 42UL, Deadline = TimeSpan.FromSeconds(30) };
```

## Common configurations

### CI — more examples, no database

```csharp
[assembly: ConjectureSettings(MaxExamples = 1000, Database = false)]
```

### Debugging — reproduce a specific failure

```csharp
[Property(Seed = 0xDEADBEEF12345678)]
public bool Debugging_this_test(int value) => ...;
```

### Per-example deadline — catch performance regressions

```csharp
[Property(DeadlineMs = 1000)]
public bool Sort_is_fast(List<int> items)
{
    MySort.Sort(items);
    return true;
}
```

### Relaxed filtering — heavy `Assume.That` usage

```csharp
[assembly: ConjectureSettings(MaxStrategyRejections = 20, MaxUnsatisfiedRatio = 500)]
```

## See also

- [How to reproduce a failure](../how-to/reproduce-a-failure.md)
- [How to manage the example database](../how-to/manage-example-database.md)
- [How to use targeted testing](../how-to/use-targeted-testing.md)
