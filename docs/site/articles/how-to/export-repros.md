# How to export counterexamples to files

When a property test fails and is shrunk, Conjecture can write the minimal counterexample to a file. This is useful for saving reproducers as CI artifacts, sharing failures with teammates, or feeding them to other tools.

## Enable export

Set `ExportReproductionOnFailure = true` in settings:

```csharp
[Property(ExportReproductionOnFailure = true)]
public bool My_property(int value) => value < 1000;
```

Or at the assembly level to export all failures:

```csharp
[assembly: ConjectureSettings(ExportReproductionOnFailure = true)]
```

## Configure the output path

By default, repros are written to `.conjecture/repros/`. Override with `ReproductionOutputPath`:

```csharp
[Property(ExportReproductionOnFailure = true, ReproductionOutputPath = "artifacts/repros/")]
public bool My_property(int value) => value < 1000;
```

Or assembly-wide:

```csharp
[assembly: ConjectureSettings(
    ExportReproductionOnFailure = true,
    ReproductionOutputPath = "artifacts/repros/")]
```

## Output format

Each exported file is named after the fully qualified test method and the failure seed:

```text
artifacts/repros/
  MyTests.My_property__seed_0xDEADBEEF.repro
```

The file contains the shrunk byte buffer that reproduces the failure. Conjecture can replay it via the `[Property(Seed = ...)]` attribute (see [How to reproduce a failure](reproduce-a-failure.md)).

## CI artifact example

In a GitHub Actions workflow:

```yaml
- name: Run tests
  run: dotnet test

- name: Upload repros
  if: failure()
  uses: actions/upload-artifact@v4
  with:
    name: conjecture-repros
    path: artifacts/repros/
```

## See also

- [Reference: Settings](../reference/settings.md) — `ExportReproductionOnFailure`, `ReproductionOutputPath`
- [How to reproduce a failure](reproduce-a-failure.md) — pin a seed to replay a failure
