# 0065. Conjecture.EFCore package design

**Date:** 2026-04-27
**Status:** Accepted

## Context

ADR 0059 ships `Conjecture.Interactions` plus `Conjecture.Http` as the v0.20 interaction foundation. ADR 0061 (`Conjecture.Messaging`), ADR 0062 (`Conjecture.Grpc`), ADR 0063 (`Conjecture.AspNetCore`), and ADR 0064 (`Conjecture.Aspire`) extend that foundation across the major transport and host layers, validating that the interaction model generalises beyond a single service.

Persistence is the next gap. EF Core is the canonical .NET data-access layer for relational models, and EF Core's `IModel` already exposes the structural metadata Conjecture needs to synthesise entities (CLR types, nullable, MaxLength, Precision, navigation properties, key relationships). Property-based testing of EF Core is doubly valuable: it exercises both the application's domain invariants under synthetic load and EF's own translation/tracking behavior under unusual but legal entity graphs.

The decision must answer:

- Ship as a separate `Conjecture.EFCore` satellite package or fold strategies into `Conjecture.Generators`?
- What is the default test provider, and how do callers override it?
- How are navigation-property cycles handled when synthesising entity graphs?
- What constraint surface is honoured in v1 (nullable, MaxLength, CheckConstraint, value generators)?
- Are strategies generated at runtime via reflection over `IModel`, or precomputed via a Roslyn source generator?
- Is LINQ query-shape fuzzing in scope for v1?
- Is migration up/down snapshot testing in scope for v1?
- How does EF Core conform to the Layer-1 (Interactions) abstraction shipped in ADR 0059, so it composes with `Conjecture.Http`, `Conjecture.Messaging`, and `Conjecture.Grpc`?

## Decision

Ship a single `Conjecture.EFCore` NuGet package containing `PropertyStrategyBuilder`, `EntityStrategyBuilder`, `EFCoreGenerate` extensions, `RoundtripAsserter`, `MigrationHarness`, plus the Layer-1 interaction surface (`DbInteraction`, `IDbTarget`, `Generate.Db`, `DbInvariantExtensions`). The package depends on `Microsoft.EntityFrameworkCore` and `Conjecture.Core`; it does not reference any test framework — framework wiring is handled by thin satellite packages per ADR 0055.

### Package topology — separate satellite, not embedded in `Conjecture.Generators`

`Conjecture.EFCore` is a standalone satellite package, not a feature of `Conjecture.Generators`. The dependency on `Microsoft.EntityFrameworkCore` (and transitively the relational provider stack) is heavy and should not be forced onto consumers who do not use EF Core. Framework adapters reference `Conjecture.EFCore` optionally per ADR 0055.

### Default test provider — SQLite in-memory, with `DbContextFactory` override

`Generate.EntitySet<T>()` and `Generate.Db.*` strategies default to building entity graphs against an EF Core `IModel` resolved from a SQLite in-memory `DbContext`. SQLite in-memory is the canonical EF testing provider: it is real SQL, supports migrations and transactions, and runs entirely in-process with no external dependency.

Callers override the provider by supplying a `DbContextFactory<TContext>` to `IDbTarget`. The override path is provider-agnostic: callers can target `InMemory`, `Sqlite`, `Npgsql`, `SqlServer`, or any provider that produces a valid `IModel`. v1 ships `InMemoryDbTarget` (EF's `Microsoft.EntityFrameworkCore.InMemory` for fast unit tests where SQL fidelity is not required) and `SqliteDbTarget` (the recommended default).

```csharp
public interface IDbTarget : IInteractionTarget
{
    DbContext Resolve(string resourceName);
    Task ResetAsync(string resourceName, CancellationToken ct = default);
}

public sealed class SqliteDbTarget : IDbTarget { ... }
public sealed class InMemoryDbTarget : IDbTarget { ... }
```

Multi-provider matrix testing (one property run executed against multiple providers in sequence) is out of scope for v1 — it can be expressed today by parameterising the test method over `IDbTarget` instances.

### Navigation cycles — bounded depth, default 2, aligned with `Generate.Recursive()`

`EntityStrategyBuilder` walks navigation properties to compose entity graphs. Cyclic navigations (e.g. `Order.Customer.Orders.Customer.…`) are bounded by a configurable depth, default `2`, mirroring the depth semantics of `Generate.Recursive()` (ADR 0042). At the depth bound, optional navigations are set to `null` and required navigations are terminated with the first generated parent (no further descent). This guarantees finite graphs without forcing the caller to declare cycle-breaking strategies.

Consumers raise the bound for deeper graphs via `Generate.EntitySet<T>().WithMaxDepth(n)` or override per-navigation via `.WithoutNavigation(e => e.Customer)`.

### Constraint scope (v1) — type-system metadata only

`PropertyStrategyBuilder` honours the type-system constraints exposed directly on `IProperty`:

- CLR type (`int`, `string`, `Guid`, `DateTime`, `decimal`, etc.)
- Nullability (`IsNullable`)
- `MaxLength` for `string` and `byte[]`
- `Precision` and `Scale` for `decimal`
- `IsConcurrencyToken` (for the concurrency invariant; no special generation)

Out of scope for v1:

- `CheckConstraint` evaluation — would require parsing relational SQL fragments per provider; deferred to a future opt-in extension.
- `HasConversion`/value converters — generated values target the CLR property type; converters are exercised on save/read but not modelled in the strategy.
- `ValueGeneratedOnAdd`/`ValueGeneratedOnUpdate` — strategies leave the property at its CLR default; EF assigns the generated value on insert. This avoids fighting EF's own value generation.
- Index uniqueness constraints across an entity set — `EntityStrategyBuilder` does not de-duplicate generated keys for unique indexes; collisions surface as `DbUpdateException` and are reported via `RoundtripAsserter`.

### Strategy generation — runtime reflection over `IModel`, no Roslyn codegen

Strategies are built at runtime by walking `IModel` and composing primitive `Strategy<T>` instances. No Roslyn source generator runs at build time. The runtime path is sufficient because:

- `IModel` is already a fully-resolved metadata object — no syntactic information is required.
- Strategy construction happens once per `Generate.EntitySet<T>()` call and is then cached per-entity-type by `EntityStrategyBuilder`, so per-example overhead is minimal.
- A source-generator path would require duplicating EF's model-building logic and would not survive `OnModelCreating` overrides.

A future codegen-only path (e.g. for AOT scenarios) is not precluded but is not delivered in v1.

### LINQ query-shape fuzzing — deferred to `Conjecture.EFCore.Linq`

Generating arbitrary `IQueryable<T>` expressions to fuzz EF's query-translation pipeline is a separate, large effort that intersects with expression-tree synthesis and provider-specific SQL emission. v1 ships entity-graph generation only. A future `Conjecture.EFCore.Linq` package will host LINQ query-shape strategies and the translation-success invariant.

### Migration up/down snapshot harness — in scope for v1

`MigrationHarness` is in scope for v1. The harness applies all pending migrations forward, snapshots the resulting schema, applies the latest migration's `Down`, then re-applies its `Up` and asserts the resulting schema matches the snapshot. This catches migrations that are not symmetric (a common cause of staging/production drift). The harness is invoked explicitly via `MigrationHarness.AssertUpDownIdempotent(ctx)` — it is not implicit in `RoundtripAsserter`.

### Owned types — generated inline with owner

EF Core owned types (`OwnsOne`, `OwnsMany`) are generated inline as part of the owner's strategy and are not exposed as standalone `Generate.EntitySet<TOwned>()` calls. This matches EF's modelling semantics: owned types have no independent identity. Callers who want to share owned-type generation across owners should compose at the property level via `PropertyStrategyBuilder`.

### Query translation invariant — deferred to v2, success-or-`InvalidOperationException`

A "queries always translate" invariant is deferred to v2. When delivered, the invariant will execute the generated query and assert that the outcome is one of:

- successful translation and execution, or
- `InvalidOperationException` (EF's documented "untranslatable expression" signal).

`NullReferenceException` or any other exception type will be reported as a failure. This shape is chosen so the invariant catches genuine translator bugs without classifying every "this LINQ shape isn't supported by this provider" message as a defect.

### Layer-1 (Interactions) conformance — third transport concretion

`Conjecture.EFCore` ships a Layer-1 surface alongside the strategy builders so EF Core is a first-class transport in the interaction model, not a parallel stateful-testing stack:

- **`DbInteraction : IInteraction`** (#488) — a record carrying `ResourceName`, `Op` (`{ Add, Update, Remove, SaveChanges, Query }`), and an opaque `Payload` (entity, key, or query specification).
- **`IDbTarget : IInteractionTarget`** (#489) — resolves a `DbContext` by `ResourceName` for the runner. `InMemoryDbTarget` and `SqliteDbTarget` are the reference implementations; both expose `ResetAsync` for inter-example state reset.
- **`Generate.Db.*`** (#490) — extension-block strategy builders (`Generate.Db.Add<T>(...)`, `Generate.Db.Update<T>(...)`, `Generate.Db.SaveChanges()`, etc.) returning `Strategy<DbInteraction>`. Composes with `InteractionStateMachine<TState>` (ADR 0033) and reuses `CommandSequenceShrinkPass` (ADR 0034).
- **`DbInvariantExtensions`** (#491) — fluent assertions mirroring `HttpInvariantExtensions`:
  - `Roundtrip()` — every `Add`/`Update`/`Remove` is observable on a fresh `DbContext`.
  - `ConcurrencyToken()` — concurrent `Update`s on the same key produce `DbUpdateConcurrencyException`.
  - `NoOrphans()` — no required relationship has a missing parent after `SaveChanges`.
  - `NoTrackingMatchesTracked()` — `AsNoTracking().FirstOrDefault(key)` agrees with the tracked entity's projection.

`CompositeInteractionTarget` (ADR 0064) routes named resources across `IHttpTarget + IDbTarget + IMessageBusTarget`. This unblocks v0.26 (AspNetCore + EFCore) and v0.27 (Aspire + EFCore) integration packages: a single `InteractionStateMachine<TState>` can interleave HTTP requests, message-queue publications, and database mutations against a unified state.

### Per-adapter test strategy

Two tiers, mirroring ADR 0061, ADR 0063, and ADR 0064:

- **Unit tier** (`Conjecture.EFCore.Tests`) — `InMemoryDbTarget` and a fake `SqliteDbTarget` substitute. Covers `PropertyStrategyBuilder` constraint mapping, `EntityStrategyBuilder` cycle bounds and required-navigation termination, `RoundtripAsserter` happy and failure paths, `MigrationHarness` symmetric and asymmetric cases, `DbInteraction`/`IDbTarget` plumbing, and shrunk-trace emission. No external SQL server.
- **Integration tier** (`Conjecture.SelfTests/EFCore/`) — real SQLite (file-backed) plus an opt-in PostgreSQL path (`EFCORE_POSTGRES_TESTS=1`). Asserts cross-provider behavior, validates the Layer-1 composition with `Conjecture.Http` against a tiny ASP.NET Core+EFCore sample, and exercises the migration harness against a multi-step migration history. Off by default in CI; runs on-demand and pre-release.

## Consequences

**Easier:**

- `IModel`-driven strategies mean callers get reasonable defaults without writing per-entity strategies — a domain with 30 entities works out of the box.
- Layer-1 conformance lets EF Core compose with HTTP and messaging in a single `InteractionStateMachine<TState>`, which is the headline win for the v0.26 and v0.27 integration packages.
- SQLite in-memory default keeps unit-tier tests hermetic and fast.
- Bounded navigation depth aligned with `Generate.Recursive()` is a single mental model for users — they already know the recursive-strategy semantics.
- Migration up/down harness catches a class of bugs that is otherwise only seen in production rollbacks.

**Harder:**

- Type-system-only constraint scope means that a property with a `CheckConstraint("Quantity > 0")` will generate values that violate the constraint and surface as `DbUpdateException`. This is acceptable for v1 (the failure mode is loud), but documentation must call it out.
- `ValueGeneratedOnAdd` properties left at their CLR default mean the strategy cannot synthesise a fully-populated entity for *unsaved* assertions — callers wanting that must call `SaveChanges` first. Documented in the reference.
- Owned-type strategies are not independently composable. Teams who want to share an owned-type strategy across two owners must drop to `PropertyStrategyBuilder`.
- `MigrationHarness` requires an `IDesignTimeDbContextFactory` or runtime-resolved `DbContext` that supports `Database.GetMigrations()`. Apps using only `EnsureCreated` will not benefit; documented as a precondition.
- The integration self-test tier requires PostgreSQL and is off by default in CI, which means provider-specific regressions may not be caught until a contributor enables the flag.

## Alternatives Considered

**Fold strategies into `Conjecture.Generators`** — ship `Generate.EntitySet<T>()` from the existing generator package. Rejected: drags `Microsoft.EntityFrameworkCore` into every consumer of `Generate.For<T>()`, including consumers who just want primitive strategies. The satellite-package pattern (ADR 0055) applies cleanly here.

**Roslyn source generator over `IModel`** — emit per-entity strategies at build time. Rejected for v1: would require re-implementing EF's model builder (which honours `OnModelCreating`, fluent configuration, and convention-based shaping), an enormous surface area to mirror correctly. Runtime reflection is sufficient and is cached after first use. Codegen path remains an option for AOT in a future release.

**Per-example `DbContext` instance** — instantiate a fresh `DbContext` for every generated example. Rejected: works but is unnecessarily expensive. `IDbTarget.ResetAsync` (transactional rollback or `SaveChanges` of compensating mutations) is fast and matches the lifecycle pattern in ADR 0064.

**Multi-provider matrix in v1** — run each property against `Sqlite + InMemory + Npgsql + SqlServer` automatically. Rejected: matrix execution is an orthogonal concern (test parameterisation, CI cost, provider availability) better handled by the test framework. Users who want matrix coverage parameterise their `[Property]` over multiple `IDbTarget` instances.

**LINQ query-shape fuzzing in v1** — synthesise arbitrary `IQueryable<T>` expressions and assert translation success. Rejected: large, deep, and provider-sensitive. Deferred to `Conjecture.EFCore.Linq` to keep v1 shippable.

**Skip migration harness — leave migrations to FluentMigrator/etc.** — defer migration testing entirely. Rejected: migrations are the highest-stakes EF surface (irreversible production changes) and the harness is small (one APIs, one round-trip). Including it in v1 raises the package's value proposition substantially with little additional cost.

**Honour `CheckConstraint` via SQL evaluation in v1** — parse the constraint expression and bias generation away from violations. Rejected: per-provider SQL parsing is a large undertaking, and the failure mode of generating values that hit the constraint (loud `DbUpdateException`) is acceptable for v1. Future opt-in via a `[CheckConstraint]` extension hook.

**Treat `CompositeInteractionTarget` as out-of-scope for `Conjecture.EFCore`** — let the integration packages (v0.26, v0.27) wire composition. Rejected: the composition pattern is a property of the Layer-1 design (ADR 0064), and EF Core's `IDbTarget` must satisfy the contract from day one. Without this, the integration packages would either bypass the Layer-1 contract or duplicate it.
