# `AspNetCoreRequestBuilder` reference

Fluent builder over a discovered ASP.NET Core endpoint surface. Produced by `Generate.AspNetCoreRequests(IHost, HttpClient)`; consumed via `.Build()` to materialise a `Strategy<HttpInteraction>`.

## Construction

```csharp
public static AspNetCoreRequestBuilder AspNetCoreRequests(IHost host, HttpClient client);
```

Extension on `Conjecture.Core.Generate`. Both arguments must be non-null and outlive the returned builder.

- `host` — typically `factory.Services.GetRequiredService<IHost>()` from a `WebApplicationFactory<TEntryPoint>`. The walker resolves `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider` from this host's service provider.
- `client` — typically `factory.CreateClient()`. Used by `HostHttpTarget` to dispatch generated `HttpInteraction` values; the builder itself only stores the reference.

## Fluent methods

### `ExcludeEndpoints(Func<DiscoveredEndpoint, bool> predicate) → AspNetCoreRequestBuilder`

Excludes every endpoint for which `predicate(ep)` returns `true`. Repeatable; multiple predicates AND together (an endpoint is excluded if **any** predicate matches). Returns the same builder so calls chain.

```csharp
.ExcludeEndpoints(static ep => ep.RequiresAuthorization)
.ExcludeEndpoints(static ep => ep.HttpMethod is "DELETE")
```

### `WithSetup(Func<Task> setupDelegate) → AspNetCoreRequestBuilder`

Installs an async setup delegate that runs before each generated example. Use for database seeding, fixture state, or per-example identity setup. The builder is persistence-agnostic; the delegate body is your responsibility.

### `ValidRequestsOnly() → AspNetCoreRequestBuilder`

Restricts synthesis to the **valid** flavour: every required parameter populated from `Generate.For<T>()`, valid `Content-Type` / `Accept` headers, well-formed body. Suppresses the malformed flavour.

### `MalformedRequestsOnly() → AspNetCoreRequestBuilder`

Restricts synthesis to the **malformed** flavour: random pick per example of missing required field, out-of-range numeric, wrong `Content-Type`, malformed JSON body, or missing required header. Suppresses the valid flavour.

### `Build() → Strategy<HttpInteraction>`

Materialises the configured strategy. The default mix (no flavour flag) emits 70 % valid and 30 % malformed. Throws `InvalidOperationException` if no endpoints remain after exclusion predicates.

## `DiscoveredEndpoint` projection

The `ExcludeEndpoints` predicate receives a `DiscoveredEndpoint` per route:

| Property | Type | Meaning |
|---|---|---|
| `DisplayName` | `string` | Human-readable endpoint identifier (controller + action, or minimal-API delegate name). |
| `HttpMethod` | `string` | `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, etc. |
| `RoutePattern` | `RoutePattern` | The raw ASP.NET Core route pattern. `RoutePattern.RawText` is the literal template. |
| `Parameters` | `IReadOnlyList<EndpointParameter>` | Every bound parameter, route + query + header + body + form, unified through `BindingSource`. |
| `ProducesContentTypes` | `IReadOnlyList<string>` | From `Produces(...)` metadata; first entry sets `Accept`. |
| `ConsumesContentTypes` | `IReadOnlyList<string>` | From `Consumes(...)` metadata; first entry sets `Content-Type`. |
| `RequiresAuthorization` | `bool` | `true` when `[Authorize]` policy metadata is present. |
| `Metadata` | `EndpointMetadataCollection` | Raw metadata for downstream filtering. |

## `EndpointParameter` projection

| Property | Type | Meaning |
|---|---|---|
| `Name` | `string` | Parameter name as bound by the route / model binder. |
| `ClrType` | `Type` | Runtime type used to select a generation strategy via `Generate.For<T>()`. |
| `Source` | `BindingSource` | `Path`, `Query`, `Header`, `Body`, `Form`, `Services`, etc. |
| `IsRequired` | `bool` | `true` when the parameter must be present in the request. |

## Errors

- **No endpoints remain after applying exclusion predicates** (`InvalidOperationException`) — every endpoint is filtered out. Loosen the predicates.
- **No `Generate.For<T>()` strategy registered** (thrown at `.Build()` time) — a route requires a parameter of type `T` that has no registered arbitrary. Add `[Arbitrary]` to `T` (and its `Conjecture.Generators` source generator runs), or register manually via `GenerateForRegistry.Register<T>(...)`.

## See also

- [Test ASP.NET Core endpoints — how-to](../how-to/test-aspnetcore-endpoints.md)
- [Why `IHost` + `HttpClient` — explanation](../explanation/aspnetcore-host-abstraction.md)
- [ADR 0063 — Conjecture.AspNetCore package design](../../decisions/0063-conjecture-aspnetcore-package-design.md)
