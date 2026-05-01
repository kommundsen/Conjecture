# Quick Start

Get a property test running in under 5 minutes.

## 1. Create a Test Project

# [xUnit v2](#tab/xunit-v2)

```bash
dotnet new xunit -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.Xunit
```

# [xUnit v3](#tab/xunit-v3)

```bash
dotnet new xunit -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.Xunit.V3
```

# [NUnit](#tab/nunit)

```bash
dotnet new nunit -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.NUnit
```

# [MSTest](#tab/mstest)

```bash
dotnet new mstest -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.MSTest
```

# [MTP](#tab/mtp)

```bash
dotnet new classlib -n MyProject.Tests
cd MyProject.Tests
dotnet add package Conjecture.TestingPlatform
```

Then edit `MyProject.Tests.csproj` and add `<OutputType>Exe</OutputType>` inside `<PropertyGroup>`.

***

## 2. Write a Property Test

Replace the default test class with:

# [xUnit v2](#tab/xunit-v2)

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

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;
using Conjecture.Xunit.V3;

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

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Core;
using Conjecture.NUnit;

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

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Core;
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MyProject.Tests;

[TestClass]
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

# [MTP](#tab/mtp)

```csharp
using Conjecture.Core;
using Conjecture.TestingPlatform;

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

***

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

# [xUnit v2](#tab/xunit-v2)

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    var sorted = items.OrderBy(x => x).ToList();
    Assert.Equal(items.Count, sorted.Count);
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    var sorted = items.OrderBy(x => x).ToList();
    Assert.Equal(items.Count, sorted.Count);
}
```

# [NUnit](#tab/nunit)

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    var sorted = items.OrderBy(x => x).ToList();
    Assert.That(sorted.Count, Is.EqualTo(items.Count));
}
```

# [MSTest](#tab/mstest)

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    var sorted = items.OrderBy(x => x).ToList();
    Assert.AreEqual(items.Count, sorted.Count);
}
```

# [MTP](#tab/mtp)

```csharp
[Property]
public void Sorting_preserves_length(List<int> items)
{
    List<int> sorted = items.OrderBy(x => x).ToList();
    if (sorted.Count != items.Count)
    {
        throw new Exception($"Expected {items.Count} items, got {sorted.Count}");
    }
}
```

***

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
    public Strategy<int> Create() => Strategy.Integers<int>(1, int.MaxValue);
}

[Property]
public bool Positive_ints_are_positive([From<PositiveInts>] int value)
{
    return value > 0;
}
```

## What's Next

- [Reference: Settings](reference/settings.md) — tune `MaxExamples`, deadlines, seeds
- [Tutorials](tutorials/01-your-first-property-test.md) — full tutorial series
- [Porting Guide](porting-guide.md) — coming from Python Hypothesis?
