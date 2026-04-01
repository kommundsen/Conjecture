# Quick Start

Get a property test running in under 5 minutes.

## 1. Create a Test Project

```bash
dotnet new xunit -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.Xunit
```

## 2. Write a Property Test

Replace the default test class with:

```csharp
using Conjecture.Core;
using Conjecture.Xunit;

namespace MyProject.Tests;

public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b)
    {
        return a + b == b + a;
    }

    [Property]
    public bool Abs_is_non_negative(int value)
    {
        // Skip int.MinValue — Math.Abs throws for it
        Assume.That(value != int.MinValue);
        return Math.Abs(value) >= 0;
    }
}
```

## 3. Run

```bash
dotnet test
```

Each `[Property]` method runs 100 times with randomly generated inputs. If a property fails, you'll see the shrunk counterexample in the test output.

## 4. See Shrinking in Action

Write a property that fails:

```csharp
[Property]
public bool All_ints_are_small(int value)
{
    return value < 1000;
}
```

Run it, and Conjecture will find a counterexample and shrink it down to `1000` — the smallest integer that violates the property.

## Return Types

Property methods can return `bool` (Conjecture asserts it's `true`) or `void` (use your framework's assertions):

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    var sorted = items.OrderBy(x => x).ToList();
    Assert.Equal(items.Count, sorted.Count);
}
```

## Explicit Examples

Use `[Example]` to add specific cases that always run before generated ones:

```csharp
[Property]
[Example(0)]
[Example(int.MaxValue)]
[Example(int.MinValue)]
public bool Abs_doesnt_throw_for(int value)
{
    Assume.That(value != int.MinValue);
    return Math.Abs(value) >= 0;
}
```

## Custom Strategies

Control how parameters are generated with `[From<T>]`:

```csharp
public class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Generate.Integers<int>(1, int.MaxValue);
}

[Property]
public bool Positive_ints_are_positive([From<PositiveInts>] int value)
{
    return value > 0;
}
```

## What's Next

- [Configuration](configuration.md) — tune `MaxExamples`, deadlines, seeds
- [Tutorials](tutorials/01-your-first-property-test.md) — full tutorial series
- [Porting Guide](porting-guide.md) — coming from Python Hypothesis?
