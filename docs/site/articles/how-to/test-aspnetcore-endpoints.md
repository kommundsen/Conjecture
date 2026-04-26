# Property-test ASP.NET Core endpoints

Use `Conjecture.AspNetCore` to walk an ASP.NET Core app's endpoint surface and synthesise valid + malformed `HttpInteraction` strategies. The package discovers minimal-API and MVC-controller routes via `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider`, dispatches through `Conjecture.Http`'s `HostHttpTarget`, and lets you assert invariants like "no valid request ever returns 5xx" and "every malformed request returns 4xx".

This how-to uses xUnit v3. The same shape works under any test runner that lets you `await` an async test method — see the per-runner table at the bottom.

## Install

```xml
<PackageReference Include="Conjecture.AspNetCore" />
<PackageReference Include="Conjecture.Xunit.V3" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
```

`Conjecture.AspNetCore` does NOT reference `Microsoft.AspNetCore.Mvc.Testing` — you supply it from your test project. The package consumes `IHost` + `HttpClient` so it composes equally well with a `WebApplicationFactory<TEntryPoint>` or an Aspire-hosted `IHost`.

## Test that valid requests never return 5xx

```csharp
using Conjecture.AspNetCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Xunit.V3;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class OrdersApiProperties : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public OrdersApiProperties(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
    }

    [Property]
    public async Task NoValidRequestReturns5xx(CancellationToken ct)
    {
        using HttpClient client = factory.CreateClient();
        IHost host = factory.Services.GetRequiredService<IHost>();
        HostHttpTarget target = new(host, client);

        Strategy<HttpInteraction> strategy = Generate
            .AspNetCoreRequests(host, client)
            .ValidRequestsOnly()
            .Build();

        await Property.ForAll(target, strategy, static async (t, request) =>
        {
            HttpResponseMessage response = await request.Response((IHttpTarget)t);
            await Task.FromResult(response).AssertNot5xx();
        }, ct: ct);
    }
}
```

The walker discovers every endpoint `Program` exposes. The synthesiser populates `[FromRoute]`, `[FromQuery]`, `[FromHeader]`, and `[FromBody]` parameters from `Gen.For<T>()` (so register an arbitrary for each DTO type via `[Arbitrary]` or `GenForRegistry.Register<T>(...)` — unregistered types throw at strategy-build time with a pointer to the missing registration). `HostHttpTarget` from `Conjecture.Http` dispatches the synthesized `HttpInteraction` through the test server.

## Test that malformed requests return 4xx, never 5xx

Pass `MalformedRequestsOnly()` to flip the synthesizer into the malformed flavour. Each generated request randomly mutates one parameter into a contract violation: missing required field, out-of-range numeric, wrong `Content-Type`, malformed JSON body, or missing required header.

```csharp
[Property]
public async Task MalformedRequestsReturn4xx(CancellationToken ct)
{
    using HttpClient client = factory.CreateClient();
    IHost host = factory.Services.GetRequiredService<IHost>();
    HostHttpTarget target = new(host, client);

    Strategy<HttpInteraction> malformed = Generate
        .AspNetCoreRequests(host, client)
        .MalformedRequestsOnly()
        .Build();

    await Property.ForAll(target, malformed, static async (t, request) =>
    {
        HttpResponseMessage response = await request.Response((IHttpTarget)t);
        await Task.FromResult(response).Assert4xx();
    }, ct: ct);
}
```

A failing test shrinks down to the minimum mutation that still triggers a 5xx — usually one specific field on one specific endpoint, surfaced by the existing `Conjecture.Core` shrinker. Diff the synthesised request against the route's contract to find the bug.

The default mix (no `ValidRequestsOnly()` / `MalformedRequestsOnly()` flag) emits 70 % valid and 30 % malformed.

## Exclude endpoints that require authorization

`DiscoveredEndpoint.RequiresAuthorization` is `true` when the endpoint carries `[Authorize]` policy metadata. Skip those in tests that do not configure a test authentication handler:

```csharp
Strategy<HttpInteraction> strategy = Generate
    .AspNetCoreRequests(host, client)
    .ExcludeEndpoints(static ep => ep.RequiresAuthorization)
    .Build();
```

`ExcludeEndpoints` is repeatable; multiple predicates AND together. Filter on `ep.HttpMethod`, `ep.RoutePattern.RawText`, or any value in `ep.Metadata`:

```csharp
Strategy<HttpInteraction> strategy = Generate
    .AspNetCoreRequests(host, client)
    .ExcludeEndpoints(static ep => ep.RequiresAuthorization)
    .ExcludeEndpoints(static ep => ep.HttpMethod is "DELETE")
    .ExcludeEndpoints(static ep => ep.RoutePattern.RawText!.StartsWith("/admin"))
    .Build();
```

## Seed database state before each example

`WithSetup(Func<Task>)` runs before each generated example. The package stays persistence-agnostic — you bring the seeding code:

```csharp
[Property]
public async Task NoValidRequestReturns5xx_WithSeededDatabase(CancellationToken ct)
{
    using HttpClient client = factory.CreateClient();
    IHost host = factory.Services.GetRequiredService<IHost>();
    HostHttpTarget target = new(host, client);

    Strategy<HttpInteraction> strategy = Generate
        .AspNetCoreRequests(host, client)
        .WithSetup(async () =>
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        })
        .ValidRequestsOnly()
        .Build();

    await Property.ForAll(target, strategy, static async (t, request) =>
    {
        HttpResponseMessage response = await request.Response((IHttpTarget)t);
        await Task.FromResult(response).AssertNot5xx();
    }, ct: ct);
}
```

For tests that mutate state (POST / PUT / DELETE), prefer one of: a `WebApplicationFactory<TEntryPoint>` per scenario, an in-memory database resetting in `WithSetup`, or `.ExcludeEndpoints(static ep => ep.HttpMethod is "POST" or "PUT" or "DELETE")` if read-only coverage is enough.

## Authenticate generated requests

The package ships zero auth code. Use the standard `WebApplicationFactory.WithWebHostBuilder` extension to register a test `AuthenticationHandler<TOptions>` and have it always succeed (or always fail, depending on the invariant under test). The generated `HttpInteraction` flows through `HttpClient`, so any default request headers the client carries are honoured by the in-process pipeline.

## Per-runner adapters

| Runner | Package | Test attribute / shape |
|---|---|---|
| xUnit v3 | `Conjecture.Xunit.V3` | `[Property]` on an `async Task(CancellationToken)` method |
| xUnit v2 | `Conjecture.Xunit` | `[Property]` on an `async Task(CancellationToken)` method |
| NUnit | `Conjecture.NUnit` | `[Property]` on an `async Task(CancellationToken)` method |
| MSTest | `Conjecture.MSTest` | `[Property]` on an `async Task(CancellationToken)` method |
| TestingPlatform | `Conjecture.TestingPlatform` | `[Property]` on an `async Task(CancellationToken)` method |
| Expecto | `Conjecture.FSharp.Expecto` | `testProperty "..." <| fun (...) -> ...` |
| .NET Interactive | `Conjecture.Interactive` + `Conjecture.Core` | imperative `await Property.ForAll(...)` in a code cell |
| LinqPad | `Conjecture.LinqPad` + `Conjecture.Core` | imperative `await Property.ForAll(target, strategy, assertion, QueryCancelToken)` |

## Interactive samples

- [`OrdersApiPropertyTests.dib`](https://github.com/kommundsen/Conjecture/blob/main/docs/samples/aspnetcore/OrdersApiPropertyTests.dib) — .NET Interactive notebook (open in VS Code with the Polyglot Notebooks extension).
- [`OrdersApiPropertyTests.linq`](https://github.com/kommundsen/Conjecture/blob/main/docs/samples/aspnetcore/OrdersApiPropertyTests.linq) — LinqPad query.

## See also

- [`AspNetCoreRequestBuilder` — reference](../reference/aspnetcore-request-builder.md)
- [Why `IHost` + `HttpClient` instead of `WebApplicationFactory<T>`](../explanation/aspnetcore-host-abstraction.md)
- [ADR 0063 — Conjecture.AspNetCore package design](../../decisions/0063-conjecture-aspnetcore-package-design.md)
- [Property-test an HTTP API from an OpenAPI document](test-http-api-with-openapi.md)
