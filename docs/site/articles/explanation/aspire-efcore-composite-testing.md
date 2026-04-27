# Why Aspire+EFCore composite testing works

`Conjecture.Aspire` exercises distributed .NET services under generated interaction loads — HTTP, messages, gRPC — across real containers provisioned by `Aspire.Hosting.Testing`. `Conjecture.EFCore` verifies entity roundtrip integrity and persistence invariants against an in-process `DbContext`. Each is a complete property-testing surface for its layer. The most expensive bugs in distributed systems live *between* the two layers: a service ACKs a message before `SaveChanges` commits, a gRPC call returns `OK` while the write silently fails, or two consumers write to overlapping rows under concurrent delivery. `Conjecture.Aspire.EFCore` exists to catch those.

## D1: Explicit factory, not DI, for context construction

`AspireDbTarget<TContext>` receives a `Func<string, TContext> contextFactory` at construction time. The factory accepts the Aspire-resolved connection string and returns a `TContext`. Every `Resolve()` call invokes the factory — one fresh context per invocation, no shared `ChangeTracker` across examples.

The alternative — resolving `TContext` from a DI container — was rejected for two reasons. First, Aspire test apps run *out of process*: there is no in-process DI container the test harness can reach into. The `DbContext` must be constructed directly in the test project against the Aspire-provisioned connection string. Second, even in scenarios where a DI container is technically accessible, binding test lifecycle to DI scope registration couples the test fixture to the production registration — a change in lifetime (Scoped → Singleton, or a connection-pool tweak) would silently break test isolation without a compilation error. The explicit factory makes the dependency explicit and keeps test-side construction under test-side control.

This mirrors the factory pattern in `Conjecture.EFCore.SqliteDbTarget` and keeps the two packages' API shapes consistent.

## D2: Per-resource targets composed via `CompositeInteractionTarget`

Each Aspire DB resource gets its own `AspireDbTarget<TContext>`. Multi-database apps construct one target per resource (`"orders-db"`, `"catalog-db"`, etc.) and register them all in `AspireDbTargetRegistry`. The registry's `ResetAllAsync` resets every target between examples.

The alternative — one aggregate `IDbTarget` for all contexts — was rejected because it loses the typed `Resolve<TContext>()` API and requires an internal dispatch switch by entity type. Separate targets keep `Resolve()` strongly typed, reset semantics explicit per resource, and `ResourceName` unambiguous for `CompositeInteractionTarget` routing (which dispatches by resource name, not by `DbContext` type).

This mirrors `AspNetCoreDbTarget<TContext>` in `Conjecture.AspNetCore.EFCore` and makes the two composite packages compositionally equivalent at the `IDbTarget` level.

## D3: `WaitForAsync` as a static extension on `IDbTarget`

Eventual-consistency polling ships as `IDbTargetWaitForExtensions.WaitForAsync`, a static extension method on `IDbTarget`. It does not add a new member to the `IDbTarget` interface.

Adding `WaitForAsync` to the interface would force every `IDbTarget` implementor — `SqliteDbTarget`, `InMemoryDbTarget`, `AspNetCoreDbTarget<TContext>`, custom implementations — to provide an eventual-consistency implementation. For in-process targets, eventual consistency is meaningless: `SaveChanges` is synchronous; there is nothing to poll. The extension method opt-in means in-process targets are unaffected, while Aspire targets gain polling without a breaking interface change.

The extension is defined in `Conjecture.Aspire.EFCore` but the extension point is `IDbTarget` — any future `IDbTarget` implementation (a Dapr state store, a Cosmos target) can be polled by taking a package reference to `Conjecture.Aspire.EFCore` without implementing a new interface member.

## Open Q1: Does eventual-consistency polling need an `IDbTarget` interface extension or belong in the test body?

Polling belongs in an extension method on `IDbTarget`, not in the test body and not as a new `IDbTarget` interface member. The answer is Decision 3: `IDbTargetWaitForExtensions.WaitForAsync` is a static extension on `IDbTarget` that ships in `Conjecture.Aspire.EFCore`. This keeps the `IDbTarget` interface stable (no new member forced on every implementor), keeps the test body free of retry loops, and makes the polling behaviour discoverable and reusable across all `IDbTarget` implementations without requiring any interface change.

## D4: No dependency on `Conjecture.AspNetCore.EFCore`

`Conjecture.Aspire.EFCore` depends on `Conjecture.EFCore` and `Conjecture.Aspire`. It does not depend on `Conjecture.AspNetCore.EFCore`.

`Conjecture.AspNetCore.EFCore` drags in `Microsoft.AspNetCore.Mvc.Testing`, which provisions an in-process `WebApplicationFactory<TEntryPoint>`. That dependency is not appropriate for out-of-process Aspire scenarios — Aspire services run in containers, not in the test process. Importing it would add a dependency that is never exercised and imposes its own lifecycle expectations (in-process host startup, in-process DI scope) that conflict with the Aspire fixture model.

The two composite packages sit at the same architectural level. Both implement `IDbTarget` from `Conjecture.EFCore` directly. The ~30 lines of `EntitySnapshotter`-based invariant logic they share are re-implemented in each package — a deliberate duplication that avoids a shared base abstraction that would couple their lifecycles and complicate future divergence.

## D5: Multi-tenancy is out of scope for v1

Multi-tenancy — where multiple connection strings or schemas exist per Aspire resource (e.g., one Postgres instance serving `tenant-a` and `tenant-b` databases) — is out of scope for v1. Each tenant is modelled as a separate Aspire resource at AppHost level (`AddDatabase("orders-tenant-a")`, `AddDatabase("orders-tenant-b")`), each getting its own `AspireDbTarget<TContext>`.

Modelling tenants as resources is the correct Aspire pattern for isolated-database tenancy. Shared-schema tenancy (row-level security, discriminator columns) requires the test harness to inject tenant context into each `DbContext` resolve — the factory pattern in D1 supports this, but the v1 API does not prescribe it. v1 ships the factory primitive; multi-tenant sequencing strategies are deferred until real demand surfaces.

## Open Q2: Why isolation invariants are deferred to v2

Cross-service isolation — writes from service A not visible to service B until committed — requires transaction-scoped `DbContext` instances and provider-specific isolation levels (PostgreSQL `SERIALIZABLE` ≠ SQL Server `SNAPSHOT`). v1 ships `WaitForAsync`, snapshot diffing, and row-count assertions only.

Isolation invariants need a model for "concurrent readers" that does not exist in the v1 sequence builder: two `DbContext` instances open simultaneously, with one observing the other's uncommitted rows (or not). Building this correctly requires provider-specific harness hooks and a way to inject concurrent readers into the `AspireStateMachine`. That complexity is deferred; v1 documents the boundary explicitly so users do not mistake `WaitForAsync` for an isolation guarantee.

## Open Q3: Why full reset semantics differ from in-process AspNetCore.EFCore

In `Conjecture.AspNetCore.EFCore`, the reset path (dropping and recreating the schema via `EnsureDeletedAsync` + `EnsureCreatedAsync`) takes milliseconds — the in-process SQLite connection is trivially resettable. In `Conjecture.Aspire.EFCore`, the same reset runs against a real Postgres or SQL Server container. Schema drops and recreations are slower (hundreds of milliseconds for large models, seconds for models with many indexes).

Snapshot-replay — rewinding to a pre-captured DB state without a full schema reset — would be faster but is impractical for containers: the container holds mutable state the Conjecture harness cannot introspect or replay from outside the container boundary. The full reset is slower but correct, and it matches the existing `AspireStateMachine` semantics for HTTP and messaging (which fully reset service state between examples). Speed optimisations (truncation instead of drop, parallel resets) are deferred to v2.

## Open Q4: Why no shared base class with `AspNetCore.EFCore`

`AspNetCoreDbTarget<TContext>` resolves `DbContext` from an in-process `IServiceScopeFactory`. `AspireDbTarget<TContext>` resolves it from an explicit factory with a connection string. The two resolution paths are fundamentally different: one navigates a DI container; the other invokes a `Func<string, TContext>`.

A shared base class would need to be abstract over the resolution mechanism — effectively an internal interface. The only shared surface would be `ExecuteAsync` dispatch and `ResetAsync`, both of which are one-liners delegating to EF Core primitives. A base class for two one-liners is not worth the surface churn it imposes on both packages when they need to diverge (e.g., when `AspNetCoreDbTarget` gains support for scoped interceptors, or `AspireDbTarget` gains connection-string refresh for token-based auth). Both classes implement `IDbTarget` directly. The test harness only sees `IDbTarget`; the implementations are independent.

## What a passing property test buys you

Two property tests — one per invariant — over a generated cross-service interaction load answer two precise questions in seconds:

- For every generated interaction that returns 4xx/5xx, is the database guaranteed unchanged after eventual settlement?
- For every interaction your contract claims is idempotent, does a replay produce identical DB state after eventual settlement?

If those two properties hold, you have ruled out the two highest-leverage bug classes in distributed message-driven systems: silent partial writes and non-idempotent consumers. Failing properties shrink to the minimal payload + step sequence that triggers the divergence; the failing example goes straight into a regression test. That ratio of cost to coverage is what property-based testing optimises for.

## See also

- [Tutorial 12: Composite property tests for Aspire + EF Core](../tutorials/12-aspire-efcore-integration.md)
- [Reference: Conjecture.Aspire.EFCore](../reference/aspire-efcore.md)
- [Explanation: Why property testing finds EF Core bugs](efcore-property-testing.md)
- [Explanation: Why Aspire uses a shared lifecycle](aspire-lifecycle.md)
- [Explanation: Why composite HTTP+DB invariants find bugs](aspnetcore-efcore-composite-testing.md)
