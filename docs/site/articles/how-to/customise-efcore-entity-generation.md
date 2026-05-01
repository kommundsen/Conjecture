# Customise EF Core entity generation

`Strategy.Entity<T>` produces structurally-valid entities from your `IModel` out of the box. This guide covers the three customisations you'll reach for first: bounding navigation depth, excluding specific navigations, and overriding individual property strategies.

## Adjust navigation depth

Navigation cycles (`Customer → Orders → Customer → …`) are bounded by `maxDepth`, default `2`. Beyond the bound: required reference navigations are populated by reusing the first generated parent of the same target type; optional reference navigations are set to `null`; collection navigations are emitted empty.

Pass `maxDepth` directly to `Strategy.Entity<T>`:

```csharp
Strategy<Customer> shallow = Strategy.Entity<Customer>(db, maxDepth: 0);
Strategy<Customer> deep    = Strategy.Entity<Customer>(db, maxDepth: 4);
```

| `maxDepth` | What you get |
|---|---|
| `0` | Scalars only. All navigations terminated at the root. Fast. |
| `1` | Direct navigations populated; second-level navigations terminated. Default for "small graphs." |
| `2` (default) | Two-level chains. Aligns with [`Strategy.Recursive()`](../tutorials/06-advanced-patterns.md#recursive-strategies) defaults. |
| `>3` | Larger graphs. Cost grows roughly linearly per added level for tree-shaped models, faster for highly-connected ones. |

> [!WARNING]
> Increasing `maxDepth` doesn't increase coverage proportionally — past depth `2` you're mostly generating the same shapes with longer tails. Prefer narrow strategies that exercise specific properties.

## Exclude a specific navigation

Drop into `EntityStrategyBuilder` when you need finer control than `maxDepth` provides:

```csharp
using Conjecture.Core;
using Conjecture.EFCore;

EntityStrategyBuilder b = new EntityStrategyBuilder(db.Model)
    .WithMaxDepth(2)
    .WithoutNavigation<Customer>(c => c.Orders)
    .WithoutNavigation<Order>(o => o.Customer);

Strategy<Customer> customers = b.Build<Customer>();
Strategy<Order>    orders    = b.Build<Order>();
```

Each `WithoutNavigation<TEntity>(expr)` call drops one navigation. Reference navigations become `null`; collection navigations become empty lists. The lambda is parsed at `Build` time — pass a member access expression like `c => c.Orders`, not a lambda body.

Use this when:

- A navigation is only meaningful in a subset of test scenarios — drop it from the strategy and add it explicitly per-test.
- A bidirectional cycle confuses your assertion (`o.Customer.Orders[0] == o`?) — drop one side.
- A navigation pulls in expensive types (e.g. `byte[]` blobs) and you don't need them for the property under test.

## Override a property's strategy

`PropertyStrategyBuilder.Build(IProperty)` honours the v1 type-system constraint scope (CLR type, nullability, `MaxLength`, `Precision`/`Scale`, `ValueGenerated`). For domain rules outside that scope — e.g. "`Customer.Email` must contain `@`" — wrap the strategy with `.Filter` or compose a domain-specific strategy:

```csharp
Strategy<Customer> domain = Strategy.Entity<Customer>(db)
    .Filter(c => c.Email.Contains('@'))
    .Map(c => c with { Email = c.Email.ToLowerInvariant() });
```

`.Filter` is the fallback. Prefer composition with strategy combinators when you can — Conjecture shrinks better through `.Map` than through `.Filter`.

For per-property strategies, build the entity manually and inject your strategy at the field site:

```csharp
Strategy<string> emails = Strategy.For<string>().Where(s => s.Contains('@'));

Strategy<Customer> customers = Generate
    .Object(() => new Customer())
    .With(c => c.Id, Strategy.Guid())
    .With(c => c.Name, Strategy.For<string>())
    .With(c => c.Email, emails);
```

(`Strategy.Object().With(...)` is the existing builder in `Conjecture.Core`. It bypasses `IModel`-driven generation entirely — you lose `MaxLength` and friends but gain full control.)

## Combining customisations

The three patterns compose. A common shape:

1. Use `EntityStrategyBuilder` for the entity-level shape (depth, dropped navigations).
2. Use `.Filter`/`.Map` at the strategy site for cross-field rules.
3. Drop to `Strategy.Object().With(...)` for individual properties that need bespoke generation.

```csharp
Strategy<Customer> baseStrategy = new EntityStrategyBuilder(db.Model)
    .WithoutNavigation<Customer>(c => c.Orders)
    .Build<Customer>();

Strategy<Customer> tailored = baseStrategy
    .Filter(c => c.JoinedAt < DateTime.UtcNow)
    .Map(c => c with { Email = c.Email.ToLowerInvariant() });
```

## See also

- [Reference: Conjecture.EFCore](../reference/efcore.md)
- [Reference: Strategy.For&lt;T&gt;()](../reference/generate-for.md)
- [Tutorial: Custom strategies](../tutorials/03-custom-strategies.md)
