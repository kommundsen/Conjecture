# Conjecture Settings API Reference

## `ConjectureSettings` Record

```csharp
public sealed record ConjectureSettings
{
    public int MaxExamples { get; init; }              // default: 100
    public ulong? Seed { get; init; }                  // null = random; set for deterministic replay
    public bool UseDatabase { get; init; }             // default: true (SQLite example cache)
    public TimeSpan? Deadline { get; init; }           // null = no per-example time limit
    public int MaxStrategyRejections { get; init; }    // default: 200
    public bool ExportReproOnFailure { get; init; }    // default: false; writes .cs repro file on failure
    public string ReproOutputPath { get; init; }       // default: ".conjecture/repros/"
}
```

## `[ConjectureSettings]` Attribute

Apply to a `[Property]` test method or test class to override defaults:

```csharp
[ConjectureSettings(MaxExamples = 500)]
[Property]
public void MyTest(int x) { ... }

// Deterministic replay (same as setting Seed):
[Property(Seed = 0xDEADBEEF)]
public void ReproduceFailure(int x) { ... }
```

### `[Property]` parameters (shorthand)

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `MaxExamples` | `int` | 100 | Number of random cases to generate |
| `Seed` | `ulong` | random | Hex literal: `0xABCD1234` |

## `[ConjectureSettings]` vs `[Property]` Seed

- `[Property(Seed = 0x...)]` — sets seed on the property attribute directly (most common for reproducing failures)
- `[ConjectureSettings(Seed = ...)]` — same effect but via the settings attribute

## Settings Profiles

Create a base settings attribute and inherit from it for consistent configuration across a test class or file:

```csharp
// Define a profile
public class HighVolumeSettings : ConjectureSettingsAttribute
{
    public HighVolumeSettings() : base(new ConjectureSettings { MaxExamples = 1000 }) { }
}

// Apply it
[HighVolumeSettings]
public class MyIntegrationTests
{
    [Property]
    public void Test1(int x) { ... }

    [Property]
    public void Test2(string s) { ... }
}
```

## Example Database

By default Conjecture stores failing examples in an SQLite database (`.conjecture/examples.db` relative to the test project). On the next run:
1. Previously failing seeds are retried first
2. If still failing, the test fails immediately without generating more examples
3. If now passing, the seed is removed from the database

Disable with `[ConjectureSettings(UseDatabase = false)]`.

## Reproduction Export

When `ExportReproOnFailure` is enabled, Conjecture writes a `.cs` file containing a deterministic `[Property(Seed = 0x...)]` snippet for each failing test:

```csharp
[ConjectureSettings(ExportReproOnFailure = true, ReproOutputPath = ".conjecture/repros/")]
[Property]
public void MyTest(int x) { ... }
```

On failure, a file is written to `ReproOutputPath` with the seed and minimal counterexample, ready to paste into a test class for deterministic replay.
