# Settings and Profiles

Conjecture's behavior is configured through `ConjectureSettings`, which can be set at the assembly level, per-test, or programmatically.

## Precedence

Settings are resolved in this order (most specific wins):

1. **`[Property]` attribute** — per-test overrides
2. **`[assembly: ConjectureSettings]`** — assembly-level defaults
3. **Built-in defaults** — `ConjectureSettings` record defaults

## Assembly-Level Settings

Set defaults for all tests in your assembly:

```csharp
// In any .cs file in your test project:
using Conjecture.Core;

[assembly: ConjectureSettings(MaxExamples = 500)]
```

Available properties on `ConjectureSettingsAttribute`:

| Property | Type | Default |
|---|---|---|
| `MaxExamples` | `int` | 100 |
| `UseDatabase` | `bool` | `true` |
| `MaxStrategyRejections` | `int` | 5 |
| `MaxUnsatisfiedRatio` | `int` | 200 |
| `DatabasePath` | `string` | `".conjecture/examples/"` |
| `Targeting` | `bool` | `true` |
| `TargetingProportion` | `double` | `0.5` |

## Per-Test Settings

Override individual settings on a `[Property]` method:

```csharp
[Property(MaxExamples = 1000, Seed = 42, DeadlineMs = 5000)]
public bool Intensive_test(List<int> items) => /* ... */;
```

`[Property]` attributes expose:

| Property | Type | Default | Notes |
|---|---|---|---|
| `MaxExamples` | `int` | 100 | |
| `Seed` | `ulong` | 0 | 0 = random seed |
| `UseDatabase` | `bool` | `true` | |
| `MaxStrategyRejections` | `int` | 5 | |
| `DeadlineMs` | `int` | 0 | 0 = no deadline; milliseconds |

## Programmatic Settings

The `ConjectureSettings` record supports `with` expressions:

```csharp
var defaults = new ConjectureSettings();
var ci = defaults with { MaxExamples = 1000, UseDatabase = false };
var debug = defaults with { Seed = 42UL, Deadline = TimeSpan.FromSeconds(30) };
```

## Common Configurations

### CI: More Examples, No Database

```csharp
[assembly: ConjectureSettings(MaxExamples = 1000, UseDatabase = false)]
```

### Development: Reproducible Failures

Fix a seed when debugging a specific failure:

```csharp
[Property(Seed = 12345)]
public bool Debugging_this_test(int value) => /* ... */;
```

The seed is reported in every failure message, so you can always copy it.

### Relaxed Filtering

If your properties use `Assume.That` or `.Where()` heavily:

```csharp
[assembly: ConjectureSettings(MaxStrategyRejections = 20, MaxUnsatisfiedRatio = 500)]
```

### Per-Example Deadline

Catch accidentally O(n!) algorithms:

```csharp
[Property(DeadlineMs = 1000)]
public bool Sort_is_fast(List<int> items)
{
    MySort.Sort(items);
    return true;
}
```
