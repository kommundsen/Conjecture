# Conjecture.Core

Property-based testing for .NET, inspired by [Hypothesis](https://github.com/HypothesisWorks/hypothesis). Generates random inputs from type-aware strategies, shrinks failing cases to a minimal counterexample, and persists failing seeds for automatic regression coverage.

The package bundles Roslyn analyzers, code fixes, and a source generator — no extra packages required.

## Install

```
dotnet add package Conjecture.Core
```

Add an adapter for your test framework: [`Conjecture.Xunit`](https://www.nuget.org/packages/Conjecture.Xunit), [`Conjecture.Xunit.V3`](https://www.nuget.org/packages/Conjecture.Xunit.V3), [`Conjecture.NUnit`](https://www.nuget.org/packages/Conjecture.NUnit), [`Conjecture.MSTest`](https://www.nuget.org/packages/Conjecture.MSTest), or [`Conjecture.TestingPlatform`](https://www.nuget.org/packages/Conjecture.TestingPlatform).

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Xunit;

public class ListTests
{
    [Property]
    public bool ReversingTwiceIsIdentity(List<int> xs)
    {
        IEnumerable<int> reversed = xs.AsEnumerable().Reverse().Reverse();
        return xs.SequenceEqual(reversed);
    }
}
```

For parameters where the default type-based strategy isn't right, supply a custom `IStrategyProvider`:

```csharp
using Conjecture.Core;
using Conjecture.Xunit;

public sealed class SmallPositiveInt : IStrategyProvider<int>
{
    public Strategy<int> Create() => Strategy.Integers<int>(1, 100);
}

public class MathTests
{
    [Property]
    public bool AdditionIsCommutative([From<SmallPositiveInt>] int a, [From<SmallPositiveInt>] int b)
    {
        return a + b == b + a;
    }
}
```

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)