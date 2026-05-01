# How to use Strategy.For&lt;T&gt;()

Use `Strategy.For<T>()` to get a `Strategy<T>` for any type decorated with `[Arbitrary]`. The source generator emits the strategy at compile time — no runtime reflection, no manual composition.

## Prerequisites

- Your type is a `partial` record, class, or struct with an accessible constructor
- All constructor parameters have auto-resolvable strategies (primitives, strings, collections, enums, or other `[Arbitrary]` types)

See [How to use source generators](use-source-generators.md) for the full list of supported types.

## Generate values for a record or class

### 1. Annotate the type

```csharp
using Conjecture.Core;

[Arbitrary]
public partial record Order(Guid Id, string Customer, decimal Total, DateOnly PlacedOn);
```

### 2. Use `Strategy.For<T>()` in a property test

# [xUnit v2](#tab/xunit-v2)

```csharp
[Property]
public bool Orders_always_have_a_customer(Order order)
{
    Strategy<Order> orders = Strategy.For<Order>();
    // or inline:
    // [From<OrderArbitrary>] Order order
    return order.Customer.Length > 0;
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
[Property]
public bool Orders_always_have_a_customer(Order order)
{
    Strategy<Order> orders = Strategy.For<Order>();
    return order.Customer.Length > 0;
}
```

# [NUnit](#tab/nunit)

```csharp
[Property]
public bool Orders_always_have_a_customer(Order order)
{
    Strategy<Order> orders = Strategy.For<Order>();
    return order.Customer.Length > 0;
}
```

# [MSTest](#tab/mstest)

```csharp
[Property]
public bool Orders_always_have_a_customer(Order order)
{
    Strategy<Order> orders = Strategy.For<Order>();
    return order.Customer.Length > 0;
}
```

***

`Strategy.For<Order>()` resolves the registered strategy. Use `[From<OrderArbitrary>]` on a parameter to feed generated `Order` values directly into the test.

## Override specific properties at the call site

Use `Strategy.For<T>(cfg => cfg.Override(...))` to substitute a different strategy for one or more properties without creating a new provider class.

```csharp
Strategy<Order> highValueOrders = Strategy.For<Order>(cfg => cfg
    .Override(o => o.Total, Strategy.Decimals(1_000m, 100_000m))
    .Override(o => o.PlacedOn, Strategy.DateOnlyValues(
        new DateOnly(2020, 1, 1),
        DateOnly.FromDateTime(DateTime.Today))));
```

Overrides compose: the rest of the properties (`Id`, `Customer`) continue to use their generated defaults.

## Apply constraint attributes

Attach generation constraints directly on constructor parameters or properties. The generator picks them up at compile time — no code changes needed at the call site.

### Constrain a numeric range

```csharp
[Arbitrary]
public partial record Product(
    string Name,
    [StrategyRange(0.01, 9_999.99)] decimal Price,
    [StrategyRange(0, 10_000)] int Stock);
```

`[StrategyRange]` applies to any numeric parameter (`int`, `long`, `double`, `decimal`, etc.).

### Constrain string length

```csharp
[Arbitrary]
public partial record Customer(
    [StrategyStringLength(1, 100)] string Name,
    [StrategyStringLength(5, 254)] string Email);
```

### Constrain string format with a regex

```csharp
[Arbitrary]
public partial record UkPostcode(
    [StrategyRegex(@"^[A-Z]{1,2}\d[A-Z\d]? \d[ABD-HJLNP-UW-Z]{2}$")] string Value);
```

> [!NOTE]
> `[StrategyRegex]` requires the `Conjecture.Regex` package to be referenced. If it is absent, the generator falls back to unconstrained strings and emits a **CON202** warning.

## Handle self-referential types

For types that reference themselves, use `[StrategyMaxDepth]` to cap generation depth.

```csharp
[Arbitrary]
[StrategyMaxDepth(3)]
public partial class TreeNode
{
    public TreeNode(int Value, TreeNode? Left, TreeNode? Right) { /* ... */ }
    public int Value { get; }
    public TreeNode? Left { get; }
    public TreeNode? Right { get; }
}
```

The generator emits a depth-bounded strategy: at `maxDepth = 0` it generates a leaf node (nullable child parameters become `null`); at greater depths it recurses.

Without `[StrategyMaxDepth]` on a self-referential type, the generator emits a **CON313** warning and uses a default depth of 5.

> [!TIP]
> Deeper trees find more edge cases but slow down generation and shrinking. Start with `[StrategyMaxDepth(3)]` and increase only if you need deeper coverage.

## See also

- [Reference: Strategy.For&lt;T&gt;()](../reference/generate-for.md) — attribute table, primitive mapping, diagnostics
- [Understanding Strategy.For&lt;T&gt;() source generation](../explanation/generate-for-source-generator.md) — why source generation and how the registry works
- [How to use source generators](use-source-generators.md) — `[Arbitrary]` basics and supported types
- [How to generate sealed class hierarchies](use-sealed-hierarchy-strategies.md) — abstract base + subtypes pattern
