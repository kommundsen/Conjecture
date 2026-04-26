# Wire `Conjecture.AspNetCore` into each test framework

The `Conjecture.AspNetCore` package itself is framework-agnostic — it produces a `Strategy<HttpInteraction>` that any test runner can consume via `Property.ForAll(target, strategy, assertion, ct)`. What differs between runners is the **fixture lifetime**: how the `WebApplicationFactory<TEntryPoint>` is constructed, shared, and disposed.

This page collects the per-runner wiring patterns. The body of each test (the `Property.ForAll` call) is identical to the [main how-to](test-aspnetcore-endpoints.md); only the fixture wrapper changes.

## xUnit v2

`IClassFixture<T>` shares one `WebApplicationFactory` per test class. xUnit v2 disposes the fixture when the class finishes.

```csharp
public class OrdersApiProperties : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public OrdersApiProperties(WebApplicationFactory<Program> factory) => this.factory = factory;

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

For an assembly-scoped fixture (one host across every test class), wrap the fixture in `[CollectionDefinition]` + `[Collection("aspnetcore")]`.

A runnable sample lives at [`src/Conjecture.AspNetCore.Tests/Samples/XunitSample.cs`](https://github.com/kommundsen/Conjecture/blob/main/src/Conjecture.AspNetCore.Tests/Samples/XunitSample.cs).

## xUnit v3

Same `IClassFixture<T>` mechanism plus `IAsyncLifetime` for async setup/teardown. xUnit v3 keeps the v2 fixture contract; the only difference is that test method signatures get a `CancellationToken` from the test framework instead of `default(CancellationToken)`.

```csharp
public class OrdersApiProperties : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> factory;

    public OrdersApiProperties(WebApplicationFactory<Program> factory) => this.factory = factory;

    public ValueTask InitializeAsync() => default;

    public ValueTask DisposeAsync() => default;

    [Property]
    public async Task NoValidRequestReturns5xx(CancellationToken ct)
    {
        // identical body to the xUnit v2 sample above
    }
}
```

For an assembly-scoped fixture in v3, use `AssemblyFixture<T>` (new in v3).

## NUnit

`[OneTimeSetUp]` runs once per `[TestFixture]` (class scope); `[OneTimeTearDown]` disposes. `[SetUpFixture]` in a top-level namespace gives assembly scope. Cancellation flows through `TestContext.CurrentContext.CancellationToken`.

```csharp
[TestFixture]
public class OrdersApiProperties
{
    private WebApplicationFactory<Program>? factory;

    [OneTimeSetUp]
    public void OneTimeSetUp() => factory = new WebApplicationFactory<Program>();

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Conjecture.NUnit.Property]
    public async Task NoValidRequestReturns5xx()
    {
        CancellationToken ct = TestContext.CurrentContext.CancellationToken;
        using HttpClient client = factory!.CreateClient();
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

## MSTest

`[ClassInitialize]` and `[ClassCleanup]` run once per `[TestClass]`. `[AssemblyInitialize]` / `[AssemblyCleanup]` for assembly scope. Cancellation through `TestContext.CancellationTokenSource.Token`.

```csharp
[TestClass]
public class OrdersApiProperties
{
    private static WebApplicationFactory<Program>? factory;

    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext _) => factory = new WebApplicationFactory<Program>();

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (factory is not null)
        {
            await factory.DisposeAsync();
        }
    }

    [Conjecture.MSTest.Property]
    public async Task NoValidRequestReturns5xx()
    {
        CancellationToken ct = TestContext.CancellationTokenSource.Token;
        using HttpClient client = factory!.CreateClient();
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

## Microsoft.Testing.Platform

TestingPlatform exposes session-scoped hooks via `ITestSessionLifetimeHandler`. The fixture lives for the session and disposes when the session ends. Cancellation flows in from the framework through every test method's `CancellationToken` parameter.

```csharp
public sealed class OrdersApiSessionFixture : ITestSessionLifetimeHandler
{
    public WebApplicationFactory<Program> Factory { get; } = new();

    public Task OnTestSessionStartingAsync(TestSessionContext _, CancellationToken __)
        => Task.CompletedTask;

    public async Task OnTestSessionFinishingAsync(TestSessionContext _, CancellationToken __)
        => await Factory.DisposeAsync();
}

[TestClass]
public class OrdersApiProperties
{
    [Property]
    public async Task NoValidRequestReturns5xx(CancellationToken ct)
    {
        WebApplicationFactory<Program> factory = SessionFixtures.OrdersApi.Factory;
        // identical Property.ForAll body to other runners
    }
}
```

Register the fixture in `TestingPlatformBuilder.AddSessionLifetimeHandler<OrdersApiSessionFixture>()` in your test entry point.

## Expecto (F#)

Expecto composes tests with `testList`. Hold the fixture in a `use` binding so the host disposes when the test list completes. The xUnit-style `IClassFixture` pattern doesn't apply — fixture lifetime is structurally controlled by where the `use` binding lives.

```fsharp
open Conjecture.AspNetCore
open Conjecture.Core
open Conjecture.Http
open Conjecture.Interactions
open Conjecture.FSharp.Expecto
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

let ordersApiTests =
    use factory = new WebApplicationFactory<Program>()
    let host = factory.Services.GetRequiredService<IHost>()
    let client = factory.CreateClient()
    let target = HostHttpTarget(host, client)

    testList "Orders API" [
        testProperty "No valid request returns 5xx" <| fun () ->
            let strategy =
                Generate
                    .AspNetCoreRequests(host, client)
                    .ValidRequestsOnly()
                    .Build()
            Property.ForAll(target, strategy, fun t request ->
                task {
                    let! response = request.Response(t :?> IHttpTarget)
                    do! response.AssertNot5xx()
                })
                .GetAwaiter().GetResult()
    ]
```

## See also

- [Test ASP.NET Core endpoints — how-to](test-aspnetcore-endpoints.md)
- [`AspNetCoreRequestBuilder` reference](../reference/aspnetcore-request-builder.md)
- [ADR 0063 — Conjecture.AspNetCore package design](../../decisions/0063-conjecture-aspnetcore-package-design.md)
