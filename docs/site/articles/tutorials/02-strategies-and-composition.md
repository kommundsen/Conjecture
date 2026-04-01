# Tutorial 2: Strategies and Composition

In [Tutorial 1](01-your-first-property-test.md), Conjecture auto-resolved strategies from parameter types. This tutorial shows you how to use strategies explicitly and compose them.

## The `Generate` Class

`Generate` is a static factory class with methods for creating strategies:

```csharp
using Conjecture.Core;

// Numeric types — generic over IBinaryInteger<T>
Generate.Integers<int>()              // full int range
Generate.Integers<int>(0, 100)        // [0, 100]
Generate.Integers<byte>()             // [0, 255]
Generate.Integers<long>(1, 1_000_000) // bounded long

// Floating point
Generate.Doubles()                    // any double (including NaN, +/-Inf)
Generate.Doubles(0.0, 1.0)           // [0.0, 1.0]
Generate.Floats()

// Other primitives
Generate.Booleans()
Generate.Bytes(16)                    // fixed-size byte array
Generate.Strings(minLength: 1, maxLength: 100)
Generate.Enums<DayOfWeek>()

// Collections
Generate.Lists(Generate.Integers<int>(), minSize: 1, maxSize: 50)
Generate.Sets(Generate.Strings())
Generate.Dictionaries(Generate.Strings(), Generate.Integers<int>())

// Fixed values
Generate.Just(42)                     // always produces 42
Generate.SampledFrom(new[] { "red", "green", "blue" })

// Alternatives
Generate.OneOf(strategyA, strategyB)  // picks one strategy per example

// Nullability
Generate.Nullable(Generate.Integers<int>()) // int or null

// Tuples
Generate.Tuples(Generate.Strings(), Generate.Integers<int>())
```

## LINQ Combinators

Strategies compose with LINQ extension methods from `StrategyExtensions`:

### `Select` — Transform Values

```csharp
// Map ints to their absolute values
Strategy<int> absInts = Generate.Integers<int>().Select(Math.Abs);
```

### `Where` — Filter Values

```csharp
// Only even numbers
Strategy<int> evens = Generate.Integers<int>(0, 1000).Where(n => n % 2 == 0);
```

> **Warning:** Don't make `Where` predicates too restrictive. If most generated values are rejected, Conjecture throws `UnsatisfiedAssumptionException`. Prefer constraining the input range instead.

### `SelectMany` — Dependent Generation

Generate a value, then use it to create another strategy:

```csharp
// Generate a list, then pick an element from it
Strategy<(List<int> list, int element)> listWithElement =
    Generate.Lists(Generate.Integers<int>(), minSize: 1)
        .SelectMany(list =>
            Generate.SampledFrom(list).Select(elem => (list, elem)));
```

### `Zip` — Pair Strategies

```csharp
Strategy<(string, int)> pairs =
    Generate.Strings().Zip(Generate.Integers<int>());

// With projection:
Strategy<KeyValuePair<string, int>> kvps =
    Generate.Strings().Zip(
        Generate.Integers<int>(),
        (k, v) => new KeyValuePair<string, int>(k, v));
```

### `OrNull` — Add Null Possibility

```csharp
Strategy<int?> maybeInt = Generate.Integers<int>().OrNull();
```

### `WithLabel` — Name for Counterexamples

```csharp
Strategy<int> ages = Generate.Integers<int>(0, 150).WithLabel("age");
```

Labels appear in failure output, making counterexamples easier to read.

## Query Syntax

Because Conjecture implements `Select`, `Where`, and `SelectMany`, you can use C# query syntax:

```csharp
var orderStrategy =
    from customerId in Generate.Integers<int>(1, 10_000)
    from itemCount in Generate.Integers<int>(1, 20)
    from items in Generate.Lists(
        Generate.Strings(minLength: 1, maxLength: 30),
        minSize: itemCount, maxSize: itemCount)
    select new Order(customerId, items);
```

## `Generate.Compose` — Imperative Style

For complex generation logic that doesn't fit LINQ, use `Generate.Compose`:

```csharp
var bstStrategy = Generate.Compose<BinarySearchTree>(ctx =>
{
    var size = ctx.Generate(Generate.Integers<int>(0, 100));
    var tree = new BinarySearchTree();
    for (int i = 0; i < size; i++)
    {
        tree.Insert(ctx.Generate(Generate.Integers<int>()));
    }
    return tree;
});
```

`IGeneratorContext` provides:
- `ctx.Generate(strategy)` — draw a value from a strategy
- `ctx.Assume(condition)` — reject the current example if `false`

## Using Custom Strategies in Tests

To use a strategy for a `[Property]` parameter, implement `IStrategyProvider<T>`:

```csharp
public class OrderStrategy : IStrategyProvider<Order>
{
    public Strategy<Order> Create() =>
        from customerId in Generate.Integers<int>(1, 10_000)
        from items in Generate.Lists(Generate.Strings(minLength: 1), minSize: 1)
        select new Order(customerId, items);
}

[Property]
public bool Orders_have_items([From<OrderStrategy>] Order order)
{
    return order.Items.Count > 0;
}
```

## Next

[Tutorial 3: Custom Strategies](03-custom-strategies.md) — deep dive into `IStrategyProvider<T>`, `[From<T>]`, and `[FromFactory]`.
