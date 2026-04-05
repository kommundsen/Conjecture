# Conjecture Test Attributes Reference

## `[Property]`

Marks a method as a Conjecture property test. Available on all adapters (xUnit, NUnit, MSTest).

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
```

**Parameters:**

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `Seed` | `ulong` | random | Hex literal for deterministic replay, e.g. `0xDEAD` |
| `MaxExamples` | `int` | 100 | Override the example count for this test |

## `[ConjectureSettings]`

Applies `ConjectureSettings` to a test method or test class. Takes precedence over defaults.

```csharp
[ConjectureSettings(MaxExamples = 500, UseDatabase = false)]
[Property]
public void HeavyTest(int x) { ... }
```

## `[Example]`

Seeds explicit, hand-written examples that are always run (in addition to random ones).

```csharp
[Property]
[Example(0)]
[Example(int.MaxValue)]
[Example(-1)]
public void MyProperty(int x) { ... }
```

Each `[Example]` value is passed directly to the first parameter. For multi-parameter tests, `[Example]` takes a params array matching the method signature types.

## `[From<T>]`

Overrides the strategy used for a specific parameter by referencing an `IStrategyProvider<TValue>` type.

```csharp
[Property]
public void Test([From<PositiveIntStrategy>] int x) { ... }

// Where:
public class PositiveIntStrategy : IStrategyProvider<int>
{
    public static Strategy<int> GetStrategy() => Generate.Integers<int>(1, 100);
}
```

## `[FromFactory]`

Overrides the strategy for a parameter by referencing a factory method name on the test class (or a named type).

```csharp
[Property]
public void Test([FromFactory(nameof(MakePositive))] int x) { ... }

private static Strategy<int> MakePositive() => Generate.Integers<int>(1, 100);
```

## Strategy Resolution Order

When Conjecture resolves a strategy for a `[Property]` parameter, it checks in this order:

1. `[From<T>]` attribute on the parameter
2. `[FromFactory]` attribute on the parameter
3. `[Example]` attribute (for explicit examples)
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
        Generate.Compose(ctx => new MyType(
            ctx.Generate(Generate.Integers<int>()),
            ctx.Generate(Generate.Strings())));
}
```

Reference `Conjecture.Generators` as an analyzer in your test project's `.csproj`:
```xml
<ProjectReference Include="..\Conjecture.Generators\Conjecture.Generators.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```
