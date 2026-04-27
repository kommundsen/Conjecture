# 0066. Conjecture.AspNetCore.EFCore package design

**Date:** 2026-04-27
**Status:** Accepted

## Context

ADR 0063 (`Conjecture.AspNetCore`) ships HTTP-shaped property tests against a live ASP.NET Core host. ADR 0065 (`Conjecture.EFCore`) ships entity-graph generation, roundtrip integrity, migration symmetry, and the Layer-1 `IDbTarget` for `DbInteraction` dispatch. v0.25.x and v0.26.0 (#582) hardened the EFCore foundation: `IDbTarget.ExecuteAsync` now dispatches `DbInteraction` for both `InMemoryDbTarget` and `SqliteDbTarget`; `EntitySnapshot` / `EntitySnapshotter` / `EntitySnapshotDiff` are reusable infrastructure in `Conjecture.EFCore`; `DbInvariantException` is the unified base for all DB-shape assertion failures.

The remaining gap is the *interaction* between the two layers. An ASP.NET Core endpoint that reads and writes a database can return `201 Created` while silently failing to flush (missed `await`, swallowed exception, incomplete transaction), or return `200 OK` while writing only half an aggregate. These bugs are invisible to single-layer tests: the HTTP response looks healthy, the DB row looks healthy, but the relationship between them is wrong. A composite HTTP+DB target surfaces them by snapshotting `DbContext` state immediately before and after each generated request and asserting persistence consistency on the response.

`Microsoft.AspNetCore.Mvc.Testing` exposes `WebApplicationFactory<TEntryPoint>`, which runs the real ASP.NET Core pipeline in process — including the EF Core `DbContext` registered in the service container. The test host can reach into the container directly to inspect DB state without a network hop, bridging the two layers cleanly.

The decision must answer:

- How does the package expose `IDbTarget` from the test host's service container, given that `DbContext`s are normally request-scoped?
- Who owns the `WebApplicationFactory<TEntryPoint>` lifecycle?
- How does the package detect endpoints that should be asserted idempotent, when HTTP verb semantics are too lossy?
- How is `DbContextPool` handled when pooled contexts can hide the very state-leak bugs the package is meant to find?
- How are multi-context applications (read-side, write-side, audit) modelled?
- Can the existing `IDbTarget` implementations from `Conjecture.EFCore` be reused as-is?
- Should this v0.26.0 package also wire up the Aspire path?
- Which backing DB engine should tests and docs target?

## Decision

Ship a single `Conjecture.AspNetCore.EFCore` NuGet package containing `AspNetCoreDbTarget<TContext>`, the composite invariant builder `AspNetCoreEFCoreInvariants` (with `AssertNoPartialWritesOnErrorAsync`, `AssertCascadeCorrectnessAsync`, `MarkIdempotent` + `AssertIdempotentAsync`), and `AspNetCoreEFCoreInvariantException` (derived from `DbInvariantException`). The package depends on `Microsoft.AspNetCore.Mvc.Testing`, `Conjecture.AspNetCore`, and `Conjecture.EFCore`; it does not reference any test framework directly — framework wiring stays in the existing satellite packages.

### Package topology — separate satellite, not embedded

`Conjecture.AspNetCore.EFCore` is a standalone satellite, not folded into either parent. The dependency on `Microsoft.AspNetCore.Mvc.Testing` is non-trivial and should not be forced onto consumers who use only one half of the stack. Consumers who need both opt in by referencing the composite package; consumers who need just HTTP property tests reference `Conjecture.AspNetCore` alone. The same satellite-package pattern as ADR 0055.

### `DbContext` access — per-call `IServiceScopeFactory.CreateScope()`

`AspNetCoreDbTarget<TContext>` resolves the test host's `DbContext` through a fresh DI scope on every `ResolveContext()` call. The constructor caches `host.Services.GetRequiredService<IServiceScopeFactory>()`; each resolution creates an `IServiceScope`, pulls the registered `TContext` from it, and returns a wrapper that disposes the scope when the context is disposed.

```csharp
public sealed class AspNetCoreDbTarget<TContext>(IHost host, string resourceName) : IDbTarget
    where TContext : DbContext
{
    private readonly IServiceScopeFactory scopeFactory =
        host.Services.GetRequiredService<IServiceScopeFactory>();

    public string ResourceName { get; } = resourceName;

    public DbContext ResolveContext(string name)
    {
        IServiceScope scope = scopeFactory.CreateScope();
        DbContext ctx = scope.ServiceProvider.GetRequiredService<TContext>();
        return new ScopedDbContext(ctx, scope);
    }

    public TContext Resolve() => (TContext)ResolveContext(ResourceName);
}
```

Direct `host.Services.GetRequiredService<TContext>()` is rejected: it returns the singleton-cached scope's context, leaking `ChangeTracker` state across all callers and masking exactly the bugs the composite package is meant to surface.

User-supplied `Func<DbContext>` (the existing pattern in `SqliteDbTarget` / `InMemoryDbTarget`) is rejected for this satellite: it forces every caller to write the scope-management code themselves, and most will get it wrong silently.

### Lifecycle ownership — caller-supplied `IHost`

`AspNetCoreDbTarget<TContext>(IHost host, string resourceName)` accepts an `IHost` from the caller and does not own the `WebApplicationFactory<TEntryPoint>` lifecycle. This mirrors `HostHttpTarget(IHost, HttpClient)` from ADR 0063: callers obtain the host once via xUnit `IClassFixture<WebApplicationFactory<TApp>>` (or NUnit `[OneTimeSetUp]`, MSTest `[ClassInitialize]`) and pass `factory.Services.GetRequiredService<IHost>()` to both `HostHttpTarget` and `AspNetCoreDbTarget<TContext>`.

The shared-factory pattern is essential: composite invariants need before/after DB snapshots correlated with the HTTP responses that triggered them. Two separate factory instances would mean two separate `DbContext` registrations, breaking the correlation. Owning the factory inside the target would block sharing and force a dedicated factory per target type.

### Idempotency detection — opt-in builder predicate

`AspNetCoreEFCoreInvariants.MarkIdempotent(Func<DiscoveredEndpoint, bool>)` is the only mechanism for declaring which endpoints should be asserted idempotent. The predicate is evaluated against `DiscoveredEndpoint` instances (already public in `Conjecture.AspNetCore`) so callers can match by route pattern, HTTP method, attribute presence, or any combination.

```csharp
AspNetCoreEFCoreInvariants invariants = new(httpTarget, dbTarget)
    .MarkIdempotent(endpoint =>
        endpoint.HttpMethod is "PUT" or "DELETE"
        || endpoint.RoutePattern.StartsWith("/api/upserts/"));
```

HTTP-verb inference (PUT/DELETE/GET-only) is rejected: POST endpoints are often idempotent (upserts), PUT can have side effects (counter increments), and DELETE is usually but not always idempotent. Verb-only inference produces false positives that mask real bugs and false negatives that suppress real assertions.

A `[Idempotent]` attribute on the action method is rejected: it couples the production app to a Conjecture-defined annotation. The test author should drive opt-in, not the production code.

### `DbContextPool` — document, don't enforce

The package does not auto-disable `AddDbContextPool` on the test host. `AspNetCoreDbTarget<TContext>` always calls `ChangeTracker.Clear()` (or its equivalent through the scoped resolution) so pooled contexts return clean to callers regardless of host configuration. The how-to documentation will recommend `services.AddDbContext<TContext>` over `AddDbContextPool<TContext>` in test entry points and explain that pooled contexts can carry configured services (interceptors, query filters, change-tracker listeners) across examples and mask state-leak bugs.

Auto-disabling pooling would require mutating the test host's DI container, which is intrusive and sometimes wrong (a property that genuinely depends on pooled-context behaviour would silently break). Per-example container teardown was rejected as too heavy: it defeats the shared-factory amortisation and pushes per-example cost from milliseconds to seconds.

### Multi-context — `AspNetCoreDbTarget<TContext>` per registered context

Generic on the class. `TContext` pins the DI service the target resolves; `IDbTarget` itself stays non-generic so `CompositeInteractionTarget` routes by `ResourceName`.

```csharp
AspNetCoreDbTarget<OrdersContext> orders = new(host, "orders-db");
AspNetCoreDbTarget<AuditContext>  audit  = new(host, "audit-db");

CompositeInteractionTarget composite = new(
    ("orders-db", orders),
    ("audit-db",  audit),
    ("http",      httpTarget));
```

Aggregating multiple contexts into a single target was rejected: it requires custom dispatch by CLR type and contradicts the existing per-resource routing model. A non-generic `AspNetCoreDbTarget(IHost, Type, string)` form was rejected for ergonomics: callers want compile-time type safety for `Resolve()`.

### Backing provider — SQLite by default

Tests, samples, and how-to docs target `SqliteDbTarget` (real SQL engine, in-process, no external infrastructure). Cascades execute through the relational pipeline production providers (PostgreSQL, SQL Server) also use, so cascade-correctness assertions catch real bugs. The integration self-test tier opts into PostgreSQL behind the existing `*_INTEGRATION_TESTS=1` gate.

The EF InMemory provider is documented as **not recommended for cascade invariants**: its in-memory cascade emulation can drift from SQL behaviour, so a passing `AssertCascadeCorrectnessAsync` against InMemory does not guarantee correctness against the production provider.

### Layer-1 composition — `IDbTarget` reuse confirmed

`IDbTarget` is reused unchanged. The new `AspNetCoreDbTarget<TContext>` implements it; `CompositeInteractionTarget` routes both `HttpInteraction` (via the existing HTTP target) and `DbInteraction` (via `AspNetCoreDbTarget<TContext>.ExecuteAsync`, which #582 ensures is implemented across all targets). A single `InteractionStateMachine<TState>` can therefore interleave HTTP requests and DB mutations as one shrinking sequence, which is the headline win for composite testing.

### Aspire integration — deferred to v0.27.0

`Conjecture.Aspire.EFCore` is out of scope for this package. Aspire's `DistributedApplication` lifecycle (containers, port allocation, health checks, retry policies) is fundamentally different from `WebApplicationFactory`'s in-process `IHost`. Mixing both into one package muddles the design and bloats the dependency graph. v0.27.0 ships a dedicated satellite that follows the same composite pattern but adapts to the Aspire fixture model (ADR 0064).

### Invariants in v1

- **`AssertNoPartialWritesOnErrorAsync(request, ct)`** — captures `EntitySnapshotter.CaptureAsync(db)` before, runs the request, captures after; if status code ≥ 400 and the diff is non-empty, throws `AspNetCoreEFCoreInvariantException` with the status code, request method+path, and diff report.
- **`AssertCascadeCorrectnessAsync(deleteRequest, rootEntityType, ct)`** — walks `IModel.GetEntityTypes()`, enumerates each `IForeignKey` whose `PrincipalEntityType.ClrType == rootEntityType`, and after the delete request asserts that dependent rows behave per the configured `DeleteBehavior` (Cascade/ClientCascade rows removed; SetNull/ClientSetNull FK columns nulled; Restrict/NoAction rows unchanged). Returns early if the request returns 4xx/5xx (composes with `AssertNoPartialWritesOnErrorAsync` for that path).
- **`AssertIdempotentAsync(request, endpoint, ct)`** — when the registered `MarkIdempotent` predicate matches the endpoint, runs the request twice; captures snapshots between and after; throws if the second snapshot differs from the first or if response status codes diverge.

All three throw `AspNetCoreEFCoreInvariantException : DbInvariantException` so `try { … } catch (DbInvariantException) { … }` catches every DB-shape failure across the EFCore stack. `EntitySnapshotter` from `Conjecture.EFCore` is the snapshot/diff helper; this satellite consumes it without re-implementing the underlying walk.

### Per-adapter test strategy

Two tiers, mirroring ADR 0061, ADR 0063, and ADR 0064:

- **Unit tier** (`Conjecture.AspNetCore.EFCore.Tests`) — `WebApplicationFactory<TestApp>` with a minimal in-process `TestApp` that registers a SQLite-in-memory `OrdersDbContext` and exposes a handful of endpoints (one happy-path, one rigged 4xx-with-partial-write, one delete cascade root). Covers all three invariants, the scoped-DI resolution path, the multi-context composition story, and the empty-or-disposed safety paths.
- **Integration tier** (`Conjecture.SelfTests/AspNetCoreEFCore/`) — real SQLite plus an opt-in PostgreSQL pathway gated on `ASPNETCORE_EFCORE_INTEGRATION_TESTS=1`. Asserts cross-provider cascade behaviour and exercises the composite trace shrinking against a small `InteractionStateMachine<TState>` driving both an HTTP endpoint and the underlying `DbContext`.

## Consequences

**Easier:**

- Single `WebApplicationFactory` per test class backs both HTTP and DB targets; before/after assertions correlate with the HTTP responses that triggered them with no extra plumbing.
- Per-call `IServiceScopeFactory` resolution makes `ChangeTracker` leakage between examples impossible by construction.
- Generic `AspNetCoreDbTarget<TContext>` gives compile-time type safety for `Resolve()` while keeping the `IDbTarget` contract uniform across the stack.
- `AspNetCoreEFCoreInvariantException : DbInvariantException` lets users catch every DB-shape failure (Roundtrip, Migration, Composite) under one base.
- Reusing `EntitySnapshotter` from `Conjecture.EFCore` means snapshot-and-diff is one well-tested helper, not three near-duplicates.

**Harder:**

- Callers using `AddDbContextPool` and depending on pooled-context behaviour must opt out manually; the package can't silently fix this for them.
- Multi-context apps must construct one target per context and compose via `CompositeInteractionTarget`. The aggregate-target ergonomics some users may want is explicitly out of scope.
- Cascade invariants are SQLite-realistic but not provider-exhaustive; tests passing locally against SQLite can still fail against a different production provider's cascade semantics. The integration self-test tier mitigates this for repository-internal coverage.
- Per-example DB reset is the user's responsibility (via `ResetAsync` on the target or app-side reset endpoints, mirroring the Aspire pattern). Forgetting to reset is the #1 source of flake — documentation will call this out prominently.

## Alternatives Considered

**Fold composite invariants into `Conjecture.AspNetCore` or `Conjecture.EFCore`.** Rejected: drags `Microsoft.AspNetCore.Mvc.Testing` (or the EF stack, depending on which side absorbs it) into consumers who use only the other half. The satellite pattern from ADR 0055 fits cleanly here.

**Direct `host.Services.GetRequiredService<TContext>()`.** Rejected: returns a singleton-cached scope's context whose `ChangeTracker` accumulates state across calls. Defeats the composite package's bug-finding mandate.

**Owned `WebApplicationFactory<TEntryPoint>` lifecycle.** Rejected: blocks shared-factory scenarios that are essential for correlated before/after snapshots and conflicts with `HostHttpTarget`'s established pattern.

**Verb-inferred idempotency.** Rejected: too lossy. Real APIs have idempotent POSTs (upserts) and non-idempotent PUTs (counter increments).

**`[Idempotent]` attribute on action methods.** Rejected: couples the production application to a Conjecture-defined annotation. Test authors should drive opt-in.

**Auto-disable `DbContextPool`.** Rejected: mutating the test host's DI is intrusive and unpredictable; properties that genuinely rely on pooled-context behaviour would silently break.

**Aggregate `AspNetCoreDbTarget` covering all `DbContext` types.** Rejected: requires custom CLR-type dispatch and conflicts with the existing per-resource-name routing model. Per-context-type targets compose cleanly through `CompositeInteractionTarget`.

**InMemory provider as the default test backend.** Rejected: in-memory cascade emulation drifts from real SQL. Cascade invariants need a real engine; SQLite gives one in-process.

**Bundle the Aspire path into v0.26.0.** Rejected: distributed-application lifecycle differs enough that one package would either compromise both APIs or bifurcate internally. v0.27.0 ships `Conjecture.Aspire.EFCore` separately.
