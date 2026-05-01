# Why `IHost` + `HttpClient` instead of `WebApplicationFactory<TEntryPoint>`

`Conjecture.AspNetCore`'s public surface accepts `IHost` and `HttpClient` separately:

```csharp
Strategy.AspNetCoreRequests(host, client);
```

It does not depend on `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<TEntryPoint>`, even though every realistic test caller in 2026 *does* construct one. This page explains the trade.

## What `WebApplicationFactory` would have given us

A direct dependency on `Microsoft.AspNetCore.Mvc.Testing` would have collapsed the call site to one argument:

```csharp
Strategy.AspNetCoreRequests(factory);   // hypothetical
```

The library could call `factory.Server.Host` to get the `IHost` and `factory.CreateClient()` to get the `HttpClient` itself. Two lines saved per test class. Convenient.

## What we would have lost

`Microsoft.AspNetCore.Mvc.Testing` is a **test-only** assembly that pulls a different transitive dependency closure than `Microsoft.AspNetCore.App`. Referencing it from `Conjecture.AspNetCore` (a production package) means:

1. Every production reference graph that imports `Conjecture.AspNetCore` for static-analysis inspection or extension-block discovery would also pull `Microsoft.AspNetCore.Mvc.Testing` along with it. Test infrastructure leaking into production references is the kind of thing that NuGet audits catch later and hate.
2. **Aspire compatibility breaks.** Aspire-hosted apps run a real `IHost` (often an `IDistributedApplicationHost`-derived shape) that is **not** a `WebApplicationFactory`. Coupling to `WebApplicationFactory` would have forced every Aspire user to build a fake factory that wraps their Aspire host, defeating the point of Aspire.
3. The same applies to any future ASP.NET Core hosting model. `IHost` is the stable contract — the `Microsoft.Extensions.Hosting` abstraction has not changed shape since .NET Core 3.0. `WebApplicationFactory` lives in a separate testing assembly and has moved more.

## The two-argument shape

The cost is two extra lines at the call site:

```csharp
HttpClient client = factory.CreateClient();
IHost host = factory.Services.GetRequiredService<IHost>();
```

In exchange:

- `Conjecture.AspNetCore` references only `Microsoft.AspNetCore.App` (the framework reference) and `Conjecture.Core` / `Conjecture.Interactions` / `Conjecture.Http`. No test-only assemblies.
- The same package serves `WebApplicationFactory`-based xUnit tests, Aspire-based integration tests, and any future hosting model that produces an `IHost` — without an adapter layer.
- `HttpClient` is decoupled from `IHost`. Tests that need a custom `HttpClient` (custom handlers, retry policies, baseline auth headers) build it themselves and pass it in. The library doesn't care how it was constructed.

## Could we have shipped a `WebApplicationFactory` extension method?

A satellite package — `Conjecture.AspNetCore.Mvc.Testing` — could provide:

```csharp
public static AspNetCoreRequestBuilder AspNetCoreRequests<T>(this Generate _, WebApplicationFactory<T> factory) where T : class
    => Strategy.AspNetCoreRequests(factory.Services.GetRequiredService<IHost>(), factory.CreateClient());
```

We did not ship that satellite for v1 — it's a one-line convenience and the upstream call site is hardly painful. If users routinely write the same six-line wrapper in every test class, a future enhancement can promote it. The architectural shape stays the same.

## Related decisions

- ADR 0063 §"Host abstraction — `IHost` + `HttpClient`, not `WebApplicationFactory`" records this decision in full.
- ADR 0059 (Conjecture.Interactions / Conjecture.Http) made the same choice for `HostHttpTarget` — `IHost` + `HttpClient` arrived first, and `Conjecture.AspNetCore` extends rather than re-litigates that contract.
- ADR 0062 (Conjecture.Grpc) used the same pattern for `HostGrpcTarget`. The three transports (HTTP, gRPC, ASP.NET Core endpoint discovery) all share the `IHost` + transport-client shape, so cross-cutting tooling (telemetry, scaffolds) handles them uniformly.

## See also

- [Test ASP.NET Core endpoints — how-to](../how-to/test-aspnetcore-endpoints.md)
- [`AspNetCoreRequestBuilder` reference](../reference/aspnetcore-request-builder.md)
- [ADR 0063 — Conjecture.AspNetCore package design](../../decisions/0063-conjecture-aspnetcore-package-design.md)
- [ADR 0059 — Conjecture.Interactions and Conjecture.Http architecture](../../decisions/0059-conjecture-interactions-and-http-architecture.md)
