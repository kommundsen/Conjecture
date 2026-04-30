# How to use the MTP adapter

`Conjecture.TestingPlatform` is a native [Microsoft Testing Platform (MTP)](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro) adapter. The test project compiles to a self-contained executable — no xUnit, NUnit, or MSTest runner needed.

## When to choose MTP

- **.NET 10+ greenfield projects** — MTP is the default going forward and requires no framework dependency.
- **Minimal dependencies** — just `Conjecture.TestingPlatform`; no third-party test framework.
- **Framework-agnostic pipelines** — no runner packages, no `.runsettings`, no adapter DLLs.

If you have an existing test suite in xUnit, NUnit, or MSTest, stay with the corresponding adapter. Conjecture's `[Property]` API is identical across all adapters.

## Project setup

Create a new class library, then add `Conjecture.TestingPlatform`:

```bash
dotnet new classlib -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.TestingPlatform
```

Edit the generated `.csproj` to set `OutputType=Exe`:

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

The package supplies the entry point — no `Program.cs` needed in your project.

## Minimal working example

```csharp
using Conjecture.Core;
using Conjecture.TestingPlatform;

public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b) => a + b == b + a;

    [Property(MaxExamples = 500)]
    public void Abs_is_non_negative(int value)
    {
        Assume.That(value != int.MinValue);
        if (Math.Abs(value) < 0)
        {
            throw new Exception("Negative abs value");
        }
    }
}
```

Run with:

```bash
dotnet test
```

Each `[Property]` method runs against 100 randomly generated inputs by default. A failure shows the shrunk counterexample.

## Configure per-test behaviour

Set `[Property]` attribute properties to control individual test runs:

```csharp
[Property(MaxExamples = 500, DeadlineMs = 200)]
public bool Reverse_preserves_length(List<int> xs) =>
    xs.AsEnumerable().Reverse().Count() == xs.Count;

[Property(Seed = 42UL, Database = false)]
public void Deterministic_property(int value)
{
    Assert.True(value < int.MaxValue);
}

[Property(ExportReproductionOnFailure = true, ReproductionOutputPath = "failures/")]
public bool Serialisation_roundtrips(string input) =>
    Deserialise(Serialise(input)) == input;
```

For the full list of properties and their defaults, see [Attributes reference](../reference/attributes.md#property).

To apply defaults across the whole assembly, use `[assembly: ConjectureSettings(...)]`:

```csharp
[assembly: ConjectureSettings(MaxExamples = 500, Database = false)]
```

> [!NOTE]
> The `--conjecture-seed` and `--conjecture-max-examples` CLI flags override the corresponding attribute values for every property in the run. See [CLI options](#cli-options) below.

## CLI options

Two CLI flags override settings globally for all properties in the run:

| Option | Description |
|--------|-------------|
| `--conjecture-seed <ulong>` | Fix a seed for deterministic replay across all properties |
| `--conjecture-max-examples <int>` | Override `MaxExamples` for all properties |

Pass them after `--` so `dotnet test` forwards them to the executable:

```bash
dotnet test -- --conjecture-seed 42
dotnet test -- --conjecture-max-examples 1000
```

## TRX reports

TRX output is supported out of the box — no extra configuration:

```bash
dotnet test --report-trx
```

Failed test nodes include the Conjecture counterexample, so the TRX captures the minimal failing input alongside the failure message.

## Next

- [Tutorial 5: Framework Adapters](../tutorials/05-framework-adapters.md) — side-by-side comparison of all adapters
- [How to reproduce a failure](reproduce-a-failure.md) — replay a specific counterexample with a fixed seed
