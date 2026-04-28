# Conjecture.Aspire

Property-based stateful testing for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) distributed applications, built on [Conjecture](https://github.com/kommundsen/Conjecture). `IAspireAppFixture` boots a `DistributedApplication`, `AspireStateMachine<TState>` models cross-resource interactions, and `AspireProperty.RunAsync` drives randomized command sequences against the live AppHost while shrinking failures to a minimal counterexample.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Aspire
```

## Usage

```csharp
using Conjecture.Aspire;
using Conjecture.Core;
using Aspire.Hosting;

public sealed class MyAppFixture : IAspireAppFixture
{
    public override async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddProject<Projects.MyApi>("api");
        return await builder.BuildAsync(ct).ConfigureAwait(false);
    }
}

public sealed record CartState(int ItemCount);

public sealed class CartMachine : AspireStateMachine<CartState>
{
    public override CartState InitialState() => new(0);

    public override IEnumerable<Strategy<Interaction>> Commands(CartState state)
    {
        yield return Generate.Just(new Interaction("api", "POST", "/cart/items", new { sku = "A" }));
    }

    public override CartState RunCommand(CartState state, Interaction cmd) =>
        state with { ItemCount = state.ItemCount + 1 };

    public override void Invariant(CartState state)
    {
        if (state.ItemCount < 0) { throw new InvalidOperationException("cart count went negative"); }
    }
}

await AspireProperty.RunAsync(
    fixture: new MyAppFixture(),
    machine: new CartMachine(),
    settings: new ConjectureSettings(),
    cancellationToken: CancellationToken.None);
```

For database side-effects, pair this package with [`Conjecture.Aspire.EFCore`](https://www.nuget.org/packages/Conjecture.Aspire.EFCore) which bridges `IDbTarget` into the same fixture lifecycle.

## Types

| Type | Role |
|---|---|
| `IAspireAppFixture` | Manages the `DistributedApplication` lifecycle (`StartAsync`, `ResetAsync`, `WaitForHealthyAsync`). |
| `AspireStateMachine<TState>` | `InitialState`, `Commands`, `RunCommand`, `Invariant`, plus `GetClient(resourceName)`. |
| `Interaction` | Readonly record describing an HTTP call to a named Aspire resource. |
| `AspireProperty.RunAsync(fixture, machine, settings, ct)` | Runs the state machine with shrinking and reset between iterations. |
| `AspireSessionLifetimeHandler` | Microsoft Testing Platform extension that boots / disposes the fixture per session. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
