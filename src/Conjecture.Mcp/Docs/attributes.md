# Conjecture Test Attributes Reference

## `[Property]`

Marks a method as a Conjecture property test. Available on all adapters (xUnit, NUnit, MSTest, and Microsoft Testing Platform).

```csharp
// xUnit
using Conjecture.Xunit;
[Property]
public void MyProperty(int x, string s) { ... }

// NUnit
using Conjecture.NUnit;
[Property]
public void MyProperty(int x, string s) { ... }

// MSTest
using Conjecture.MSTest;
[Property]
public void MyProperty(int x, string s) { ... }

// Microsoft Testing Platform
using Conjecture.TestingPlatform;
[Property]
public void MyProperty(int x, string s) { ... }
```

**Parameters (xUnit / NUnit / MSTest adapters):**

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `Seed` | `ulong` | random | Hex literal for deterministic replay, e.g. `0xDEAD` |
| `MaxExamples` | `int` | 100 | Override the example count for this test |

**Parameters (Microsoft Testing Platform adapter — `Conjecture.TestingPlatform`):**

`PropertyAttribute` in the MTP adapter carries the **full settings surface directly** — no separate `[ConjectureSettings]` attribute exists in that namespace.

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `Seed` | `ulong?` | `null` (random) | Fixed seed for deterministic replay |
| `MaxExamples` | `int` | `100` | Number of examples to generate |
| `Database` | `bool` | `true` | Whether to use the SQLite example cache |
| `MaxStrategyRejections` | `int` | `5` | Max strategy rejections per value |
| `DeadlineMs` | `int` | `0` | Per-example deadline in ms; `0` = no deadline |
| `Targeting` | `bool` | `true` | Whether to run a targeting phase |
| `TargetingProportion` | `double` | `0.5` | Fraction of budget for targeting |
| `ExportReproductionOnFailure` | `bool` | `false` | Write reproduction file on failure |
| `ReproductionOutputPath` | `string` | `".conjecture/repros/"` | Output path for reproduction files |

## `[ConjectureSettings]`

Applies `ConjectureSettings` to a test method or test class. Takes precedence over defaults.

```csharp
[ConjectureSettings(MaxExamples = 500, Database = false)]
[Property]
public void HeavyTest(int x) { ... }
```

## `[Sample]`

Seeds explicit, hand-written examples that are always run (in addition to random ones).

```csharp
[Property]
[Sample(0)]
[Sample(int.MaxValue)]
[Sample(-1)]
public void MyProperty(int x) { ... }
```

Each `[Sample]` value is passed directly to the first parameter. For multi-parameter tests, `[Sample]` takes a params array matching the method signature types.

## `[From<T>]`

Overrides the strategy used for a specific parameter by referencing an `IStrategyProvider<TValue>` type.

```csharp
[Property]
public void Test([From<PositiveIntStrategy>] int x) { ... }

// Where:
public class PositiveIntStrategy : IStrategyProvider<int>
{
    public static Strategy<int> GetStrategy() => Strategy.Integers<int>(1, 100);
}
```

## `[FromFactory]`

Overrides the strategy for a parameter by referencing a factory method name on the test class (or a named type).

```csharp
[Property]
public void Test([FromFactory(nameof(MakePositive))] int x) { ... }

private static Strategy<int> MakePositive() => Strategy.Integers<int>(1, 100);
```

## Strategy Resolution Order

When Conjecture resolves a strategy for a `[Property]` parameter, it checks in this order:

1. `[From<T>]` attribute on the parameter
2. `[FromFactory]` attribute on the parameter
3. `[Sample]` attribute (for explicit examples)
4. `IStrategyProvider<T>` registered via the source generator (`[Arbitrary]`)
5. Built-in strategies (primitives, collections)
6. `ConjectureException` if no strategy found

## `[Arbitrary]` (Source Generator)

Applied to a partial class that implements `IStrategyProvider<T>`. The source generator weaves in registration code so the strategy is auto-discovered.

```csharp
[Arbitrary]
public partial class MyTypeStrategies : IStrategyProvider<MyType>
{
    public static Strategy<MyType> GetStrategy() =>
        Strategy.Compose(ctx => new MyType(
            ctx.Generate(Strategy.Integers<int>()),
            ctx.Generate(Strategy.Strings())));
}
```

Reference `Conjecture.Generators` as an analyzer in your test project's `.csproj`:
```xml
<ProjectReference Include="..\Conjecture.Generators\Conjecture.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```