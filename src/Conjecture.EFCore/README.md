# Conjecture.EFCore

Property-based testing for Entity Framework Core, built on [Conjecture.NET](https://github.com/kommundsen/Conjecture).

Derives entity-graph strategies mechanically from `IModel`, asserts `SaveChanges` roundtrip integrity, verifies migration up/down symmetry, and exposes `DbInteraction`/`IDbTarget` so EF Core composes with HTTP, gRPC, and messaging under a single `InteractionStateMachine<TState>`.

```csharp
[Property]
public async Task Order_Roundtrips_Without_Loss()
{
    Strategy<Order> orders = Generate.Entity<Order>(factory.Create);
    Order order = orders.Sample();
    await RoundtripAsserter.AssertRoundtripAsync(factory.Create, order);
}
```

See [ADR 0065](../../docs/decisions/0065-conjecture-efcore-package-design.md) for design.
