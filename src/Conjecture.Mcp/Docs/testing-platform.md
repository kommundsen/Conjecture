# Conjecture.TestingPlatform Adapter Reference

## What is Conjecture.TestingPlatform

`Conjecture.TestingPlatform` is a native `ITestFramework` for [Microsoft Testing Platform (MTP)](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro). It is **not** a VSTestBridge adapter — it runs as a self-contained test executable with `OutputType=Exe`.

This means no xunit.runner, no MSTest runner, and no separate test host. The compiled test project is itself the test runner.

## Project Setup

Add a `PackageReference` to `Conjecture.TestingPlatform` and set `OutputType=Exe`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Conjecture.TestingPlatform" Version="*" />
  </ItemGroup>
</Project>
```

No additional runner package is needed.

## `[Property]` Attribute

Mark methods as property-based tests with `[Property]` from the `Conjecture.TestingPlatform` namespace:

```csharp
using Conjecture.TestingPlatform;

[Property]
public void MyProperty(int x, string s) { ... }
```

`PropertyAttribute` in the MTP adapter carries the **full settings surface directly** — no separate `[ConjectureSettings]` attribute exists in this namespace.

### Parameters

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `Seed` | `ulong?` | `null` (random) | Fixed seed for deterministic replay, e.g. `0xDEAD` |
| `MaxExamples` | `int` | `100` | Number of examples to generate |
| `Database` | `bool` | `true` | Whether to use the SQLite example cache |
| `MaxStrategyRejections` | `int` | `5` | Max times a strategy may reject a generated value |
| `DeadlineMs` | `int` | `0` | Per-example time limit in milliseconds; `0` means no deadline |
| `Targeting` | `bool` | `true` | Whether to run a targeting phase after generation |
| `TargetingProportion` | `double` | `0.5` | Fraction of `MaxExamples` budget allocated to targeting |
| `ExportReproductionOnFailure` | `bool` | `false` | Write a reproduction file on test failure |
| `ReproductionOutputPath` | `string` | `".conjecture/repros/"` | Output directory for reproduction files |

### Example with settings

```csharp
using Conjecture.TestingPlatform;

[Property(MaxExamples = 500, Database = false, DeadlineMs = 200)]
public void ParseRoundtrip(string input) { ... }

// Deterministic replay from a previous failure:
[Property(Seed = 0xABCD1234)]
public void ReproduceBug(int x) { ... }
```

## CLI Options

When running under `Conjecture.TestingPlatform`, two CLI flags override settings globally for all properties in the run:

| Option | Type | Description |
|--------|------|-------------|
| `--conjecture-seed <ulong>` | `ulong` | Force a fixed seed for all property runs |
| `--conjecture-max-examples <int>` | `int` (positive) | Override `MaxExamples` for all properties |

### Passing CLI options

Via `dotnet run`:
```bash
dotnet run -- --conjecture-seed 42
dotnet run -- --conjecture-max-examples 1000
```

Via `dotnet test` (MTP runner):
```bash
dotnet test -- --conjecture-seed 42
dotnet test -- --conjecture-max-examples 1000
```

## TRX Reports

`Conjecture.TestingPlatform` implements `ITrxReportCapability` automatically. No extra configuration is needed in the `.csproj`.

Enable TRX output at the command line:
```bash
dotnet test --report-trx
```

Failed test nodes include the Conjecture counterexample message, so the TRX report captures the minimal failing input alongside the failure.

## CrashDump Support

Add `Microsoft.Testing.Extensions.CrashDump` to your test project — it is auto-wired by MSBuild:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="*" />
</ItemGroup>
```

No code changes are required. A `.dmp` file is written next to the test output on unhandled crash.
