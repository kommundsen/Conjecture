# Getting Started

This page provides a brief overview of property-based testing with Conjecture.NET. For a hands-on walkthrough, see the [Quick Start](quick-start.md).

## What is Property-Based Testing?

Traditional unit tests verify specific examples:

```csharp
[Fact]
public void Reverse_twice_returns_original()
{
    var list = new List<int> { 1, 2, 3 };
    list.Reverse();
    list.Reverse();
    Assert.Equal(new[] { 1, 2, 3 }, list);
}
```

Property-based tests verify a **property** that holds for *all* valid inputs:

```csharp
[Property]
public bool Reverse_twice_returns_original(List<int> items)
{
    var copy = new List<int>(items);
    copy.Reverse();
    copy.Reverse();
    return items.SequenceEqual(copy);
}
```

Conjecture generates hundreds of random lists and checks that the property holds for each one. If it finds a failure, it **shrinks** the input to the smallest counterexample — for instance, a single-element list that triggers the bug.

## Key Concepts

### Strategies

A `Strategy<T>` knows how to generate random values of type `T`. Conjecture provides built-in strategies for primitives, strings, collections, and more via the `Generate` class:

```csharp
Generate.Integers<int>()              // random ints
Generate.Integers<int>(0, 100)        // ints in [0, 100]
Generate.Strings(maxLength: 50)       // random ASCII strings
Generate.Lists(Generate.Booleans())   // lists of bools
```

### Automatic Resolution

For `[Property]` test method parameters, Conjecture automatically resolves strategies by type. You don't need to specify a strategy for common types — just declare the parameter:

```csharp
[Property]
public bool Addition_is_commutative(int a, int b)
{
    return a + b == b + a;
}
```

### LINQ Composition

Strategies compose using LINQ, letting you build complex generators from simple ones:

```csharp
var positiveEvenInts = Generate.Integers<int>(1, 1000)
    .Where(n => n % 2 == 0);

var personStrategy =
    from name in Generate.Strings(minLength: 1, maxLength: 50)
    from age in Generate.Integers<int>(0, 150)
    select new Person(name, age);
```

### Shrinking

When a property fails, Conjecture doesn't just report the first counterexample it finds. It systematically **shrinks** the input, searching for the smallest, simplest input that still triggers the failure. This happens automatically — no shrink functions to write.

## Next Steps

- [Quick Start](quick-start.md) — write and run your first property test
- [Tutorials](tutorials/01-your-first-property-test.md) — progressive tutorial series
- <xref:Conjecture.Core?text=API+Reference> — full generated API docs
