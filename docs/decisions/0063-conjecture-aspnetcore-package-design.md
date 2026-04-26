# 0063. Conjecture.AspNetCore package design

**Date:** 2026-04-26
**Status:** Accepted

## Context

ADR 0059 ships `Conjecture.Interactions` plus `Conjecture.Http` as the v0.20 interaction foundation. ADR 0061 (`Conjecture.Messaging`) and ADR 0062 (`Conjecture.Grpc`) extend that foundation to the second and third transports, and validate that Layer 1 generalises beyond request/response.

ASP.NET Core minimal APIs and MVC controllers are the front door of almost every modern .NET service. Model binding bugs — a nullable DTO property binding as `default` instead of returning `400`, a `[FromRoute]` vs `[FromQuery]` mismatch, a `JsonIgnoreCondition` combination that accepts malformed payloads — surface as `500 InternalServerError` in production rather than clean `400 BadRequest`. Property-based testing against `WebApplicationFactory` systematically explores the request surface and pins the contract: "for any generated request matching the DTO schema, the endpoint returns 2xx; for any generated malformed request, it returns 400, never 500".

The `Conjecture.Http` package (ADR 0059) already supplies `HttpInteraction`, `IHttpTarget`, and `HostHttpTarget` for in-process testing of an `IHost`. What `Conjecture.Http` does **not** do is *discover* the endpoint surface of an ASP.NET Core app and synthesise typed requests for each route — it accepts a hand-written `Strategy<HttpInteraction>` and dispatches it. `Conjecture.AspNetCore` is the metadata-driven layer on top: walk `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider`, derive a `Strategy<HttpInteraction>` per endpoint, and let users write the high-level invariants.

The decision must answer:

- What is the package's public dependency surface — does it bind to `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory`, or stay at `IHost` + `HttpClient`?
- How are minimal APIs and MVC controllers discovered uniformly?
- How does request synthesis compose with `Generate.For<T>()` (#73) for typed DTOs and with the existing `HttpInteraction` shape (ADR 0059)?
- How is auth handled for endpoints behind `[Authorize]`?
- How are middleware-short-circuited endpoints (authz, rate limiting) treated?
- How are user-managed test fixtures (DB seeding, authenticated identity) plugged in?
- How does OpenAPI metadata (`Microsoft.AspNetCore.OpenApi`, #73) integrate?
- What is the per-adapter test strategy?

## Decision

Ship a single `Conjecture.AspNetCore` NuGet package containing the endpoint walker, the request synthesiser, the fluent `AspNetCoreRequestBuilder`, and the `Generate.AspNetCoreRequests()` extension block. The package is metadata-driven: it consumes `IHost` + `HttpClient` (so it composes with both `WebApplicationFactory<TEntryPoint>` and Aspire-hosted `IHost` instances) and produces `Strategy<HttpInteraction>` instances dispatched by the existing `HostHttpTarget` from `Conjecture.Http`.

### Host abstraction — `IHost` + `HttpClient`, not `WebApplicationFactory`

The package depends on `Microsoft.AspNetCore.App` (for `EndpointDataSource`, `IApiDescriptionGroupCollectionProvider`, and the routing types) and `Microsoft.AspNetCore.Mvc.Core`. It does **not** reference `Microsoft.AspNetCore.Mvc.Testing`. Users supply `WebApplicationFactory<TEntryPoint>` themselves — its `Server.Host` provides the `IHost` and `CreateClient()` provides the `HttpClient` — and pass both into `Generate.AspNetCoreRequests(host, client)`.

This keeps the package Aspire-compatible from day 1: an Aspire-hosted `IHost` (which does not derive from `WebApplicationFactory`) plugs in identically. It also avoids dragging the Mvc.Testing test-only assembly into production reference graphs.

### Endpoint discovery — dual walker

Use **both** `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider`, merged at runtime:

- `EndpointDataSource` (DI-resolved from `IHost.Services`) returns the canonical post-build endpoint set. It covers minimal APIs (`MapGet`, `MapPost`, `MapGroup`) and the new endpoint-routing world uniformly. Each `Endpoint` exposes `RoutePattern`, `Metadata` (HTTP methods, `[FromBody]` parameter types, `[Authorize]` policies, `Produces`/`Consumes` content types).
- `IApiDescriptionGroupCollectionProvider` (also DI-resolved) returns `ApiDescription` records for MVC controllers, including conventional routing, parameter sources (`BindingSource.Path`, `BindingSource.Query`, `BindingSource.Body`), and response type metadata.

The walker emits a `DiscoveredEndpoint` projection that union-merges both sources, deduplicating by `(HttpMethod, RoutePattern)`. Minimal APIs surface only via `EndpointDataSource`; controllers surface via both, but the `ApiDescription` view is preferred when present because it carries richer parameter metadata. Endpoints that surface in `EndpointDataSource` only (e.g. minimal APIs without explicit metadata) fall back to walking the route pattern + delegate signature.

```csharp
public sealed record DiscoveredEndpoint(
    string DisplayName,
    string HttpMethod,
    RoutePattern RoutePattern,
    IReadOnlyList<EndpointParameter> Parameters,
    IReadOnlyList<string> ProducesContentTypes,
    IReadOnlyList<string> ConsumesContentTypes,
    bool RequiresAuthorization,
    EndpointMetadataCollection Metadata);

public sealed record EndpointParameter(
    string Name,
    Type ClrType,
    BindingSource Source,           // Path | Query | Header | Body | Form | Services
    bool IsRequired);
```

`BindingSource` is the existing `Microsoft.AspNetCore.Mvc.ModelBinding.BindingSource` enum — re-using it instead of a parallel enum keeps the mapping from MVC metadata trivial.

### Request synthesis — `RequestSynthesizer`

Per discovered endpoint, `RequestSynthesizer` produces two `Strategy<HttpInteraction>` flavours:

- **Valid** — every required parameter populated from `Generate.For<TParameter>()`; `[FromBody]` payload populated from `Generate.For<TBody>()`; `Content-Type` set to the first `Consumes` entry (default `application/json`); `Accept` set to the first `Produces` entry.
- **Malformed** — randomly one of: missing required field, out-of-range numeric, wrong `Content-Type`, malformed JSON in body, missing required header. Used for the `never-5xx` invariant.

Both strategies emit `HttpInteraction` records (the existing Layer 1 shape from ADR 0059) so `HostHttpTarget` dispatches them unchanged. No new interaction shape, no new target — the entire dispatch path is reused.

DTO synthesis delegates to `Generate.For<T>()` (#73) and registered `[Arbitrary]` providers. For primitive parameter types (`int`, `string`, `Guid`, `DateOnly`) the synthesiser falls back to built-in `Strategy<T>` primitives. Unknown parameter types throw at strategy-build time with a clear message pointing at the parameter, the endpoint, and the `Generate.For<T>()` registration the user is missing.

### Fluent `AspNetCoreRequestBuilder`

```csharp
Strategy<HttpInteraction> strategy = Generate.AspNetCoreRequests(host, client)
    .ExcludeEndpoints(ep => ep.RoutePattern.RawText!.StartsWith("/admin"))
    .ExcludeEndpoints(ep => ep.RequiresAuthorization)
    .WithSetup(async () => await SeedDatabaseAsync())
    .ValidRequestsOnly();
```

- `.ExcludeEndpoints(Func<DiscoveredEndpoint, bool>)` — opt-out filter, repeatable, AND'd together.
- `.WithSetup(Func<Task>)` — runs before each generated example. Conjecture stays persistence-agnostic: callers supply DB seeding, identity setup, fixture state.
- `.ValidRequestsOnly()` / `.MalformedRequestsOnly()` — restrict synthesis flavour. Default emits a 70/30 valid/malformed mix.
- `.Build()` materialises `Strategy<HttpInteraction>`.

### Auth — user-supplied test handler

No built-in auth scheme, no `DefaultAuthenticationOptions`. Users plug in via the standard `WebApplicationFactory.WithWebHostBuilder(b => b.ConfigureTestServices(...))` pattern, registering a test `AuthenticationHandler<TOptions>`. The package ships zero auth code — this matches the messaging package's "user-supplied test handler" precedent (ADR 0061) and avoids prescribing an identity model.

For property tests that explicitly want to assert "anonymous requests against `[Authorize]` endpoints return 401, never 500", users compose `.ExcludeEndpoints(ep => !ep.RequiresAuthorization)` and assert directly.

### Middleware short-circuiting

All endpoints are included by default. A request that hits a 401 / 403 / 429 from `[Authorize]` or rate-limiting middleware **is** a valid invariant target — "never 5xx" still applies. Users who want to skip those endpoints use `.ExcludeEndpoints(ep => ep.RequiresAuthorization)` or a custom predicate against `ep.Metadata`.

The walker does **not** attempt to detect or model individual middleware. The contract is endpoint-shaped, not pipeline-shaped.

### OpenAPI integration — deferred to a separate sub-issue

`AspNetCoreRequestBuilder.FromOpenApi(OpenApiDocument)` ships in a follow-up sub-issue (#478) so v1 of `Conjecture.AspNetCore` can ship without an OpenAPI dependency on the critical path. The OpenAPI walker shares infrastructure with the schema-driven generation issue (#73) and reuses the same `EndpointParameter` / `DiscoveredEndpoint` projection — it is an alternate **source** of the same internal model, not a parallel pipeline.

WebSocket and SSE endpoints are out of scope for v1; the dual walker filters them out by `Endpoint.Metadata` inspection (`IRequestSizeLimitMetadata`, `WebSocketAcceptContext` markers).

### Test framework wiring — satellite extension projects

Per ADR 0055 (extension blocks for satellite packages), each test framework gets a thin extension that re-exposes `Generate.AspNetCoreRequests` under its own `Property.ForAllAsync` runner:

- `Conjecture.AspNetCore.Xunit` / `.Xunit.V3` — `[Property]` integration via the existing xUnit runners.
- `Conjecture.AspNetCore.NUnit` / `.MSTest` — `[ConjectureProperty]` integration via the NUnit and MSTest adapters.
- `Conjecture.AspNetCore.TestingPlatform` / `.Expecto` — runner-level integration.

Each is a single-class project that depends on `Conjecture.AspNetCore` plus its framework's existing `Conjecture.<Framework>` package. No new generation logic in any of them.

### MCP scaffold tool

`scaffold-aspnetcore-property-test` lives in `Conjecture.Mcp` next to the existing `scaffold-property-test` and `scaffold-messaging-property-test` tools. Inputs: target project path, optional endpoint filter regex. Outputs: a `[Property]`-attributed test method body that calls `Generate.AspNetCoreRequests(factory.Server.Host, factory.CreateClient())` with the right `using` directives and a sensible default invariant assertion (`AssertNever5xx`).

### Per-adapter test strategy

Two tiers, mirroring ADR 0061 and 0062:

- **Unit tier** (`Conjecture.AspNetCore.Tests`) — fake `IHost` whose service provider returns a stub `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider`. Covers walker dedup logic, the `BindingSource → Strategy` map, fluent builder semantics, and the request flavour mix. Uses the in-memory `TestServer` for round-trip tests against a tiny minimal-API app. No network.
- **Integration tier** (`Conjecture.SelfTests/AspNetCore/`) — real `WebApplicationFactory<Program>` against a multi-endpoint sample app (minimal API + MVC controller + authorized endpoint + DTO body endpoint). Asserts the never-5xx invariant on the sample app, verifies shrinking minimises down to a single failing field, and checks `WithSetup` runs before each example. Runs on every PR; no env-var gate (no external dependencies).

## Consequences

**Easier:**

- Same `Property.ForAll(target, strategy, assertion, ct)` shape as HTTP, messaging, and gRPC — no new primitives.
- `HttpInteraction` and `HostHttpTarget` from ADR 0059 are reused unchanged; the package adds *strategy synthesis* over an existing dispatch path.
- `ExcludeEndpoints` + `WithSetup` cover the realistic test-fixture surface without coupling to any persistence model.
- DTO synthesis falls out of `Generate.For<T>()` automatically; users get typed-body coverage by registering arbitraries they likely already have.
- Aspire compatibility costs nothing because the public surface is `IHost` + `HttpClient`.

**Harder:**

- Walking *both* `EndpointDataSource` and `IApiDescriptionGroupCollectionProvider` requires deduplication logic. Acceptable: the merge rule ("`ApiDescription` wins when it covers an endpoint") is documented and unit-tested, and minimal-API-only apps short-circuit the merge entirely.
- `BindingSource` is an MVC enum but is repurposed for minimal API parameters too. Acceptable: it covers every binding source the v1 walker recognises (Path, Query, Header, Body, Form, Services); minimal API parameter modelling maps cleanly via the `IParameterBindingMetadata` reflection that ASP.NET Core already does.
- Minimal API endpoints that use custom binding (`BindAsync(HttpContext)`) cannot have their parameters synthesised. The walker emits a `Conjecture.AspNetCore.Diagnostics.UnknownBinding` warning and excludes those endpoints from generation; users opt them in by writing a hand-rolled `Strategy<HttpInteraction>` if needed.

**Risks:**

- ASP.NET Core 10 (currently in preview) is reorganising parts of the endpoint metadata pipeline; the walker is pinned to the .NET 9 + .NET 10 RTM API surface. Tracked in the integration tier — if the metadata APIs shift, the SelfTests catch it before users do.
- `Generate.For<T>()` is the v0.22 release — `Conjecture.AspNetCore` ships in v0.23 and hard-depends on it. If a user has not registered an arbitrary for a body DTO type, strategy-build throws at startup. The error message carries the parameter, endpoint, and registration the user is missing; documentation walks through the typical fix.
- Synthesised valid requests will exercise endpoints that mutate state (POST /orders, DELETE /users/{id}) by default. Documentation makes this loud: tests must either run against a fresh `WebApplicationFactory` per scenario, use `.WithSetup` to roll back, or `.ExcludeEndpoints` mutating routes. No automatic mutation detection — the package does not parse endpoint side-effects.

## Alternatives Considered

**Couple to `WebApplicationFactory<TEntryPoint>` directly** — make `Generate.AspNetCoreRequests<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory)` the entry point. Rejected: forces a `Microsoft.AspNetCore.Mvc.Testing` reference into the production package, breaks Aspire compatibility, and wires the test-only assembly into every consumer. The `IHost` + `HttpClient` two-arg shape costs one extra line at the call site (`factory.Server.Host, factory.CreateClient()`) and buys decoupling forever.

**Single-source endpoint discovery via `EndpointDataSource` only** — skip `IApiDescriptionGroupCollectionProvider`. Rejected: MVC conventional routing (`MapControllerRoute("default", "{controller}/{action}/{id?}")`) does not expose typed parameter metadata via `EndpointDataSource`; only `IApiDescriptionGroupCollectionProvider` knows that `id` is `int?` and `action` is the method name. Single-source forces the user to hand-write a strategy for any classic-MVC parameter, which defeats the package's purpose.

**Build an OpenAPI-first walker (drop endpoint metadata)** — synthesise requests purely from the app's `OpenApiDocument`. Rejected for v1: OpenAPI generation is opt-in (`AddOpenApi()`) and not present in the average ASP.NET Core test target; many apps' `OpenApiDocument` lags behind their actual endpoint set; and OpenAPI lacks a clean expression of `[FromHeader]` requirements vs body schema. Endpoint metadata is the source of truth in the running app. OpenAPI ships as a *second* source via `.FromOpenApi(...)` (#478) for users who specifically want schema-driven generation.

**Auto-discover and instantiate an `IAuthenticationHandler` test double** — ship a built-in `Conjecture.AspNetCore.Auth.TestAuthenticationHandler` that bypasses every `[Authorize]` policy. Rejected: identity is too app-specific. A test handler that always succeeds masks real authorization bugs; one that always fails makes the never-5xx invariant trivially uninteresting. Users who want a test handler write a five-line one matching their own policy needs — the docs include a copy-paste template — and Conjecture stays out of the auth model.

**Mutation-aware request filtering** — parse endpoint method bodies (or attribute markers) to skip mutating endpoints automatically. Rejected: requires Roslyn analysis at runtime, only catches static mutations, and `[Authorize]`/`[HttpPost]` together is not a reliable mutation signal. The fluent builder's `.ExcludeEndpoints(ep => ep.HttpMethod is "POST" or "PUT" or "DELETE")` line costs nothing and is honest about what it does.

**Embed test-framework wiring in the core package** — collapse `Conjecture.AspNetCore.Xunit`, `.NUnit`, etc. into `Conjecture.AspNetCore` proper. Rejected: violates ADR 0055 (extension blocks for satellite packages), drags every test framework's runtime into every consumer, and breaks the package's "core engine, framework satellites" pattern that messaging, gRPC, and HTTP all follow.
