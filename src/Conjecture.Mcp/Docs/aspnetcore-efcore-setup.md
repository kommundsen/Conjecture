# Conjecture.AspNetCore.EFCore Setup Guide

Step-by-step guide for wiring up `Conjecture.AspNetCore.EFCore` in a test project.

See also: ADR 0066 for the design rationale behind the composite target approach.

## 1. Add the NuGet package

```xml
<PackageReference Include="Conjecture.AspNetCore.EFCore" />
```

## 2. Wire up the test fixture

Use `WebApplicationFactory<TApp>` as an `IClassFixture<>` to host the application under test:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Conjecture.AspNetCore.EFCore;

public class OrdersTests : IClassFixture<WebApplicationFactory<TestApp>>
{
    private readonly WebApplicationFactory<TestApp> _factory;

    public OrdersTests(WebApplicationFactory<TestApp> factory)
    {
        _factory = factory;
    }
}
```

## 3. Construct `HostHttpTarget` and `AspNetCoreDbTarget<TContext>`

Create both targets from the same `IHost` so HTTP requests and database state share the same service scope:

```csharp
using Conjecture.AspNetCore;
using Conjecture.AspNetCore.EFCore;

IHost host = _factory.Server.Host;
HostHttpTarget httpTarget = new(host);
AspNetCoreDbTarget<AppDbContext> dbTarget = new(host);
```

## 4. Assert invariants with `AspNetCoreEFCoreInvariants`

Use the three invariant methods to validate the endpoint/database interaction:

```csharp
using Conjecture.AspNetCore.EFCore;

// Verify that a failing request does not leave partial writes in the database
await AspNetCoreEFCoreInvariants.AssertNoPartialWritesOnErrorAsync(httpTarget, dbTarget, request);

// Verify that cascading deletes/updates are consistent
await AspNetCoreEFCoreInvariants.AssertCascadeCorrectnessAsync(httpTarget, dbTarget, request);

// Verify that re-applying the same request produces the same database state
await AspNetCoreEFCoreInvariants.AssertIdempotentAsync(httpTarget, dbTarget, request);
```

## 5. Apply the `[Property]` attribute

Use the standard `[Property]` attribute from your test framework adapter:

```csharp
[Property]
public async Task OrderEndpoint_IsIdempotent(Strategy<CreateOrderRequest> requestStrategy)
{
    CreateOrderRequest request = requestStrategy.Example();
    IHost host = _factory.Server.Host;
    HostHttpTarget httpTarget = new(host);
    AspNetCoreDbTarget<AppDbContext> dbTarget = new(host);

    await AspNetCoreEFCoreInvariants.AssertIdempotentAsync(httpTarget, dbTarget, request);
}
```
