# Tutorial 5: Framework Adapters

Conjecture works with all major .NET test frameworks. The `[Property]` attribute and core API are identical across all adapters — only the package name and attribute namespace differ.

## Side-by-Side Comparison

### xUnit v2

```csharp
using Conjecture.Xunit;

public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b) => a + b == b + a;

    [Property(MaxExamples = 500)]
    public void Abs_is_non_negative(int value)
    {
        Assume.That(value != int.MinValue);
        Assert.True(Math.Abs(value) >= 0);
    }
}
```

Package: `Conjecture.Xunit`

### xUnit v3

```csharp
using Conjecture.Xunit.V3;

public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b) => a + b == b + a;

    [Property(MaxExamples = 500)]
    public void Abs_is_non_negative(int value)
    {
        Assume.That(value != int.MinValue);
        Assert.True(Math.Abs(value) >= 0);
    }
}
```

Package: `Conjecture.Xunit.V3`

### NUnit

```csharp
using Conjecture.NUnit;

public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b) => a + b == b + a;

    [Property(MaxExamples = 500)]
    public void Abs_is_non_negative(int value)
    {
        Assume.That(value != int.MinValue);
        Assert.That(Math.Abs(value), Is.GreaterThanOrEqualTo(0));
    }
}
```

Package: `Conjecture.NUnit`

### MSTest

```csharp
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MathTests
{
    [Property]
    public bool Addition_is_commutative(int a, int b) => a + b == b + a;

    [Property(MaxExamples = 500)]
    public void Abs_is_non_negative(int value)
    {
        Assume.That(value != int.MinValue);
        Assert.IsTrue(Math.Abs(value) >= 0);
    }
}
```

Package: `Conjecture.MSTest`

> **Note:** MSTest requires `[TestClass]` on the class. The other frameworks don't.

## Shared Features

All adapters support the same `[Property]` properties:

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxExamples` | `int` | 100 | Examples to generate |
| `Seed` | `ulong` | 0 (random) | Fixed seed for reproducibility |
| `UseDatabase` | `bool` | `true` | Persist failing examples |
| `MaxStrategyRejections` | `int` | 5 | Max filter rejections per strategy |
| `DeadlineMs` | `int` | 0 (none) | Per-example timeout in ms |

All adapters also support:
- `[Example(...)]` — explicit test cases
- `[From<T>]` — custom strategy providers
- `[FromFactory("Method")]` — factory methods
- `Assume.That(condition)` — filtering
- Automatic strategy resolution from parameter types
- Byte-stream shrinking

## Choosing a Framework

The choice is usually dictated by your existing test suite. Conjecture's `[Property]` tests coexist with your regular `[Fact]`/`[Test]`/`[TestMethod]` tests in the same project.

## Next

[Tutorial 6: Advanced Patterns](06-advanced-patterns.md) — source generators, settings, and the example database.
