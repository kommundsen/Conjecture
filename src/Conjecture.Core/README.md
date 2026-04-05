# Conjecture.Core

Property-based testing for .NET, inspired by [Hypothesis](https://github.com/HypothesisWorks/hypothesis).

## Install

```
dotnet add package Conjecture.Core
```

Includes bundled Roslyn analyzers, code fixes, and a source generator — no extra packages needed.

## Usage

Add a test adapter for your framework (`Conjecture.Xunit`, `Conjecture.NUnit`, or `Conjecture.MSTest`), then write property tests:

```csharp
[Property]
public bool ReversingTwiceIsIdentity(List<int> xs)
{
    var reversed = xs.AsEnumerable().Reverse().Reverse().ToList();
    return xs.SequenceEqual(reversed);
}

[Property]
public bool AdditionIsCommutative(
    [From<IntegerStrategy>] int a,
    [From<IntegerStrategy>] int b)
{
    return a + b == b + a;
}
```

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
