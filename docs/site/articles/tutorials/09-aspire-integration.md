# Tutorial 9: Property tests for Aspire apps

This tutorial walks you through writing a property-based test against a multi-service .NET Aspire application. You will:

1. Add `Conjecture.Aspire` to your test project
2. Implement `IAspireAppFixture` to manage the app lifecycle
3. Write an `AspireStateMachine<TState>` that explores cross-service interactions
4. Run the property and read a shrunk failure trace

## Prerequisites

- .NET 10 SDK
- A working .NET Aspire app host project (`Projects.MyStore_AppHost`)
- A test project referencing `Conjecture.Xunit` or another adapter

## Install

```xml
<PackageReference Include="Conjecture.Aspire" />
<PackageReference Include="Conjecture.Aspire.Xunit" />
```

> [!NOTE]
> `Conjecture.Aspire.Xunit` (and its equivalents for NUnit, MSTest, and MTP) wire the Aspire runner into your test framework's `[Property]` attribute. The core types (`IAspireAppFixture`, `AspireStateMachine<TState>`) ship in `Conjecture.Aspire` and are framework-agnostic.

## Step 1: Implement `IAspireAppFixture`

`IAspireAppFixture` owns the `DistributedApplication` lifecycle. Override `StartAsync` to build and start the app, and `ResetAsync` to restore state between examples.

```csharp
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Conjecture.Aspire;

public class StoreAppFixture : IAspireAppFixture, IAsyncLifetime
{
    public async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        DistributedApplicationTestingBuilder appHost =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.MyStore_AppHost>(ct);

        DistributedApplication app = await appHost.BuildAsync(ct);
        await app.StartAsync(ct);
        return app;
    }

    public async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
    {
        // Restore state between examples: truncate tables, clear queues, etc.
        // Here we send a DELETE /admin/reset request to the store service.
        using HttpClient client = app.CreateHttpClient("store-api");
        await client.DeleteAsync("/admin/reset", ct);
    }

    // xUnit requires IAsyncLifetime for class-fixture teardown
    public Task InitializeAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

Key points:
- `StartAsync` is called **once per property run**, not once per example. Container startup cost is amortised across all examples.
- `ResetAsync` is called **before each example** (except the first). Use it to restore any state the previous example mutated.
- You do not need to stop or restart the app between examples — only reset its observable state.

## Step 2: Write `AspireStateMachine<TState>`

`AspireStateMachine<TState>` extends `IStateMachine<TState, Interaction>` with Aspire-specific plumbing. Override the four abstract methods to define your model.

The type parameter `TState` is your model of the system under test. Start simple — an `int` or a small record is fine.

```csharp
using Conjecture.Aspire;
using Conjecture.Core;

public class OrderFlowMachine : AspireStateMachine<int>
{
    // Start with zero orders in the system
    public override int InitialState() => 0;

    // The commands available at this state
    public override IEnumerable<Strategy<Interaction>> Commands(int orderCount)
    {
        // Always allow placing a new order
        yield return Generate
            .Constant(new Interaction("store-api", "POST", "/orders",
                new { ProductId = 1, Quantity = 1 }));

        // Only allow cancellation if an order exists
        if (orderCount > 0)
        {
            yield return Generate
                .Constant(new Interaction("store-api", "DELETE", $"/orders/{orderCount}", null));
        }
    }

    // Execute the command and return the next state
    public override int RunCommand(int orderCount, Interaction cmd)
    {
        using HttpClient client = GetClient(cmd.ResourceName);
        HttpResponseMessage response = client
            .SendAsync(new HttpRequestMessage(new HttpMethod(cmd.Method), cmd.Path))
            .GetAwaiter().GetResult();

        response.EnsureSuccessStatusCode();
        return cmd.Method == "POST" ? orderCount + 1 : orderCount - 1;
    }

    // Assert invariants after each command
    public override void Invariant(int orderCount)
    {
        // The order count must never go negative
        if (orderCount < 0)
        {
            throw new InvalidOperationException($"Order count went negative: {orderCount}");
        }
    }
}
```

`GetClient(resourceName)` returns an `HttpClient` connected to the named Aspire resource. The name matches the resource name in your AppHost project.

## Step 3: Write the property test

# [xUnit v2](#tab/xunit-v2)

```csharp
using Conjecture.Aspire.Xunit;
using Xunit;

public class OrderFlowProperties : IClassFixture<StoreAppFixture>
{
    private readonly StoreAppFixture fixture;

    public OrderFlowProperties(StoreAppFixture fixture)
        => this.fixture = fixture;

    [Property]
    public async Task OrderCountNeverGoesNegative(CancellationToken ct)
    {
        await Property.ForAspire(fixture, new OrderFlowMachine(), ct: ct);
    }
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Aspire.Xunit.V3;
using Xunit;

public class OrderFlowProperties(StoreAppFixture fixture)
    : IClassFixture<StoreAppFixture>
{
    [Property]
    public async Task OrderCountNeverGoesNegative(CancellationToken ct)
    {
        await Property.ForAspire(fixture, new OrderFlowMachine(), ct: ct);
    }
}
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Aspire.NUnit;
using NUnit.Framework;

[TestFixture]
public class OrderFlowProperties
{
    private StoreAppFixture fixture = null!;

    [OneTimeSetUp]
    public async Task Setup() => fixture = new StoreAppFixture();

    [ConjectureProperty]
    public async Task OrderCountNeverGoesNegative(CancellationToken ct)
    {
        await Property.ForAspire(fixture, new OrderFlowMachine(), ct: ct);
    }

    [OneTimeTearDown]
    public async Task Teardown() => await fixture.DisposeAsync();
}
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Aspire.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class OrderFlowProperties
{
    private static StoreAppFixture fixture = null!;

    [ClassInitialize]
    public static async Task Setup(TestContext _) => fixture = new StoreAppFixture();

    [ConjectureProperty]
    public async Task OrderCountNeverGoesNegative(CancellationToken ct)
    {
        await Property.ForAspire(fixture, new OrderFlowMachine(), ct: ct);
    }

    [ClassCleanup]
    public static async Task Cleanup() => await fixture.DisposeAsync();
}
```

***

## Step 4: Run and read a failure

When the property fails, Conjecture shrinks the interaction sequence to the minimal reproduction:

```text
Falsified after 7 examples (17 commands).
Shrunk to 2 commands:

  POST /orders { ProductId: 1, Quantity: 1 }
    → state: 1

  DELETE /orders/1
    → state: 0

  DELETE /orders/0                       ← invariant violated here
    → InvalidOperationException: Order count went negative: -1

Service logs (store-api):
  [12:34:56] DELETE /orders/0 — 200 OK (should have been 404)
```

The shrunk trace shows exactly which two commands reproduce the bug: the service accepted a `DELETE` on a non-existent order instead of returning 404. Service logs for the involved resources are captured automatically.

> [!TIP]
> Reproduce the exact failure with `--seed <N>`. The seed appears at the top of the output:
> `Seed: 12345678`. Pass `[ConjectureSettings(Seed = 12345678)]` on the test method to pin it.

## Next steps

- [How to reset database state between examples](reset-aspire-state.md)
- [How to configure retry policy for flaky containers](configure-aspire-retry.md)
- [Reference: IAspireAppFixture and AspireStateMachine](../reference/aspire.md)
- [Explanation: Why Aspire uses a shared lifecycle](../explanation/aspire-lifecycle.md)
