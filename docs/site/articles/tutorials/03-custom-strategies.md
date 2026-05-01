# Tutorial 3: Custom Strategies

This tutorial covers the different ways to provide custom strategies for `[Property]` test parameters.

## Automatic Resolution

By default, Conjecture resolves strategies from parameter types. This works for:

- All `IBinaryInteger<T>` types: `int`, `long`, `byte`, `short`, `uint`, etc.
- `bool`, `double`, `float`, `string`
- `T?` where `T : struct`
- `List<T>`, `IReadOnlySet<T>`, `IReadOnlyDictionary<TKey, TValue>`
- `(T1, T2)`, `(T1, T2, T3)`, `(T1, T2, T3, T4)` tuples
- Enums

For any type Conjecture can't auto-resolve, you need to provide a strategy.

## Option 1: `IStrategyProvider<T>` with `[From<T>]`

Create a class that implements `IStrategyProvider<T>`:

```csharp
public class EmailAddress : IStrategyProvider<string>
{
    public Strategy<string> Create() =>
        from local in Strategy.Strings(minLength: 1, maxLength: 20)
        from domain in Strategy.SampledFrom(new[] { "example.com", "test.org", "mail.net" })
        select $"{local}@{domain}";
}
```

Apply it to a parameter:

```csharp
[Property]
public bool Emails_contain_at_sign([From<EmailAddress>] string email)
{
    return email.Contains('@');
}
```

### Providers for Your Own Types

For domain types, the provider can live alongside the type:

```csharp
public record Money(decimal Amount, string Currency);

public class MoneyStrategy : IStrategyProvider<Money>
{
    public Strategy<Money> Create() =>
        from amount in Strategy.Integers<int>(0, 100_000).Select(x => (decimal)x / 100)
        from currency in Strategy.SampledFrom(new[] { "USD", "EUR", "GBP", "NOK" })
        select new Money(amount, currency);
}

[Property]
public bool Money_amount_is_non_negative([From<MoneyStrategy>] Money money)
{
    return money.Amount >= 0;
}
```

## Option 2: `[FromMethod]` — Static Factory Methods

If you prefer keeping the strategy close to the test, use a static factory method:

```csharp
public class PaymentTests
{
    static Strategy<Money> PositiveMoney() =>
        from amount in Strategy.Integers<int>(1, 100_000).Select(x => (decimal)x / 100)
        from currency in Strategy.SampledFrom(new[] { "USD", "EUR" })
        select new Money(amount, currency);

    [Property]
    public bool Payment_preserves_currency([FromMethod("PositiveMoney")] Money money)
    {
        var payment = new Payment(money);
        return payment.Amount.Currency == money.Currency;
    }
}
```

The method must be `static`, return `Strategy<T>`, and be defined on the test class.

## Option 3: `[Arbitrary]` Source Generator

For types you control, add `[Arbitrary]` to auto-derive a strategy at compile time:

```csharp
[Arbitrary]
public partial record Point(double X, double Y);
```

The source generator creates a `PointArbitrary : IStrategyProvider<Point>` class. Use it with `[From<PointArbitrary>]`:

```csharp
[Property]
public bool Distance_is_non_negative(
    [From<PointArbitrary>] Point a,
    [From<PointArbitrary>] Point b)
{
    return Distance(a, b) >= 0;
}
```

See [How to use source generators](../how-to/use-source-generators.md) for details.

## Composing Providers

Providers can use other strategies internally:

```csharp
public class NonEmptyListOfMoney : IStrategyProvider<List<Money>>
{
    public Strategy<List<Money>> Create() =>
        Strategy.Lists(new MoneyStrategy().Create(), minSize: 1, maxSize: 20);
}
```

## When to Use What

| Approach | Best for |
|---|---|
| Automatic resolution | Primitive types, standard collections |
| `[From<T>]` with `IStrategyProvider<T>` | Reusable strategies shared across tests |
| `[FromMethod]` | One-off strategies specific to a test class |
| `[Arbitrary]` source generator | Types you own with simple constructor patterns |

## Next

[Tutorial 4: Shrinking Explained](04-shrinking-explained.md) — understand how Conjecture minimizes counterexamples.
