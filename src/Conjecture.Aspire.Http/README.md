# Conjecture.Aspire.Http

HTTP interaction bridge for [Conjecture.Aspire](https://www.nuget.org/packages/Conjecture.Aspire). Resolves HTTP clients directly from a running `DistributedApplication` and provides a convenience `AspireHttpProperty.RunAsync` entry point so you don't need to wire up the target factory manually.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Aspire
dotnet add package Conjecture.Http
dotnet add package Conjecture.Aspire.Http
```

## Usage

```csharp
using Conjecture.Aspire;
using Conjecture.Aspire.Http;
using Conjecture.Core;
using Conjecture.Http;

public sealed class MyAppFixture : IAspireAppFixture
{
    public override async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        IDistributedApplicationBuilder builder = DistributedApplication.CreateBuilder();
        builder.AddProject<Projects.MyApi>("api");
        return await builder.BuildAsync(ct);
    }
}

public sealed record CartState(int ItemCount);

public sealed class CartMachine : InteractionStateMachine<CartState>
{
    public override CartState InitialState() => new(0);

    public override IEnumerable<Strategy<IInteraction>> Commands(CartState state)
    {
        yield return Strategy.Just<IInteraction>(
            new HttpInteraction("api", "POST", "/cart/items", new { sku = "A" }, null));
    }

    public override CartState RunCommand(
        CartState state, IInteraction interaction, IInteractionTarget target, CancellationToken ct)
    {
        target.ExecuteAsync(interaction, ct).GetAwaiter().GetResult();
        return state with { ItemCount = state.ItemCount + 1 };
    }

    public override void Invariant(CartState state)
    {
        if (state.ItemCount < 0) throw new InvalidOperationException("cart count went negative");
    }
}

await AspireHttpProperty.RunAsync(
    fixture: new MyAppFixture(),
    machine: new CartMachine(),
    settings: new ConjectureSettings(),
    cancellationToken: CancellationToken.None);
```

To use a custom target factory (e.g. composite multi-host routing), call `AspireProperty.RunAsync` directly with your own `Func<DistributedApplication, IInteractionTarget>`.

## Types

| Type | Role |
|---|---|
| `DistributedApplicationHttpTarget` | `IHttpTarget` that resolves `HttpClient` instances via `app.CreateHttpClient(resourceName)`. |
| `AspireHttpProperty.RunAsync` | Convenience wrapper — constructs a `DistributedApplicationHttpTarget` per example and delegates to `AspireProperty.RunAsync`. |
| `HttpInteractionTraceReporter` | Records `HttpInteraction` steps and formats them as a human-readable failure trace. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
