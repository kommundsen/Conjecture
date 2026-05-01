# Tutorial 2: Strategies and Composition

In [Tutorial 1](01-your-first-property-test.md), Conjecture auto-resolved strategies from parameter types. This tutorial shows you how to use strategies explicitly and compose them.

## The `Generate` Class

`Generate` is a static factory class with methods for creating strategies:

```csharp
using Conjecture.Core;

// Numeric types — generic over IBinaryInteger<T>
Strategy.Integers<int>()              // full int range
Strategy.Integers<int>(0, 100)        // [0, 100]
Strategy.Integers<byte>()             // [0, 255]
Strategy.Integers<long>(1, 1_000_000) // bounded long

// Floating point
Strategy.Doubles()                    // any double (including NaN, +/-Inf)
Strategy.Doubles(0.0, 1.0)           // [0.0, 1.0]
Strategy.Floats()

// Other primitives
Strategy.Booleans()
Strategy.Bytes(16)                    // fixed-size byte array
Strategy.Strings(minLength: 1, maxLength: 100)
Strategy.Enums<DayOfWeek>()

// Collections
Strategy.Lists(Strategy.Integers<int>(), minSize: 1, maxSize: 50)
Strategy.Sets(Strategy.Strings())
Strategy.Dictionaries(Strategy.Strings(), Strategy.Integers<int>())

// Fixed values
Strategy.Just(42)                     // always produces 42
Strategy.SampledFrom(new[] { "red", "green", "blue" })

// Alternatives
Strategy.OneOf(strategyA, strategyB)  // picks one strategy per example

// Nullability
Strategy.Nullable(Strategy.Integers<int>()) // int or null

// Tuples
Strategy.Tuples(Strategy.Strings(), Strategy.Integers<int>())
```

## LINQ Combinators

Strategies compose with LINQ extension methods from `StrategyExtensions`:

### `Select` — Transform Values

```csharp
// Map ints to their absolute values
Strategy<int> absInts = Strategy.Integers<int>().Select(Math.Abs);
```

### `Where` — Filter Values

```csharp
// Only even numbers
Strategy<int> evens = Strategy.Integers<int>(0, 1000).Where(n => n % 2 == 0);
```

> **Warning:** Don't make `Where` predicates too restrictive. If most generated values are rejected, Conjecture throws `UnsatisfiedAssumptionException`. Prefer constraining the input range instead.

### `SelectMany` — Dependent Generation

Generate a value, then use it to create another strategy:

```csharp
// Generate a list, then pick an element from it
Strategy<(List<int> list, int element)> listWithElement =
    Strategy.Lists(Strategy.Integers<int>(), minSize: 1)
        .SelectMany(list =>
            Strategy.SampledFrom(list).Select(elem => (list, elem)));
```

### `Zip` — Pair Strategies

```csharp
Strategy<(string, int)> pairs =
    Strategy.Strings().Zip(Strategy.Integers<int>());

// With projection:
Strategy<KeyValuePair<string, int>> kvps =
    Strategy.Strings().Zip(
        Strategy.Integers<int>(),
        (k, v) => new KeyValuePair<string, int>(k, v));
```

### `OrNull` — Add Null Possibility

```csharp
Strategy<int?> maybeInt = Strategy.Integers<int>().OrNull();
```

### `WithLabel` — Name for Counterexamples

```csharp
Strategy<int> ages = Strategy.Integers<int>(0, 150).WithLabel("age");
```

Labels appear in failure output, making counterexamples easier to read.

## Query Syntax

Because Conjecture implements `Select`, `Where`, and `SelectMany`, you can use C# query syntax:

```csharp
var orderStrategy =
    from customerId in Strategy.Integers<int>(1, 10_000)
    from itemCount in Strategy.Integers<int>(1, 20)
    from items in Strategy.Lists(
        Strategy.Strings(minLength: 1, maxLength: 30),
        minSize: itemCount, maxSize: itemCount)
    select new Order(customerId, items);
```

## `Strategy.Compose` — Imperative Style

For complex generation logic that doesn't fit LINQ, use `Strategy.Compose`:

```csharp
var bstStrategy = Strategy.Compose<BinarySearchTree>(ctx =>
{
    var size = ctx.Generate(Strategy.Integers<int>(0, 100));
    var tree = new BinarySearchTree();
    for (int i = 0; i < size; i++)
    {
        tree.Insert(ctx.Generate(Strategy.Integers<int>()));
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
        from customerId in Strategy.Integers<int>(1, 10_000)
        from items in Strategy.Lists(Strategy.Strings(minLength: 1), minSize: 1)
        select new Order(customerId, items);
}

[Property]
public bool Orders_have_items([From<OrderStrategy>] Order order)
{
    return order.Items.Count > 0;
}
```

## Next

[Tutorial 3: Custom Strategies](03-custom-strategies.md) — deep dive into `IStrategyProvider<T>`, `[From<T>]`, and `[FromMethod]`.
