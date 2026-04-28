# Conjecture.EFCore

Property-based testing for Entity Framework Core, built on [Conjecture](https://github.com/kommundsen/Conjecture). Derives entity-graph strategies mechanically from `IModel`, asserts `SaveChanges` roundtrip integrity, verifies migration up/down symmetry, and exposes `DbInteraction` / `IDbTarget` so EF Core composes with HTTP, gRPC, and messaging under a single `InteractionStateMachine<TState>`.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.EFCore
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.EFCore;
using Conjecture.Xunit;

public class OrderTests
{
    [Property]
    public async Task Order_Roundtrips_Without_Loss()
    {
        Func<DbContext> factory = () => new MyContext(/* ... */);
        Strategy<Order> orders = Generate.Entity<Order>(factory);
        Order order = orders.Sample();

        await RoundtripAsserter.AssertRoundtripAsync(factory, order);
    }
}
```

For composed transports, dispatch `DbInteraction` blocks (`Generate.Db.Add<T>(...)`, `.Update<T>(...)`, `.SaveChanges(...)`, `.Sequence(...)`) through an `IDbTarget` such as `InMemoryDbTarget` or `SqliteDbTarget`, then assert with `AssertNoOrphansAsync`, `AssertConcurrencyTokenRespectedAsync`, or `AssertNoTrackingMatchesTrackedAsync`.

## Design

See [ADR 0065](https://github.com/kommundsen/Conjecture/blob/main/docs/decisions/0065-conjecture-efcore-package-design.md) for the metadata-driven strategy derivation and invariant catalogue.

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
