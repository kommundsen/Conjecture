# Why property testing finds EF Core bugs

EF Core sits between domain code and SQL. Most of its surface is metadata — `IModel`, `IEntityType`, `IProperty`, `INavigation`, `IForeignKey` — all eagerly resolved by the time your `DbContext` finishes building. Conjecture turns that metadata into strategies, which means you can synthesise structurally-valid entity graphs without writing per-entity factories. The interesting consequence is what those strategies *find*.

## Three classes of bugs cluster at the EF boundary

### 1. Model–domain divergence

Your domain says "every `Order` has a customer." Your `OnModelCreating` says `entity.HasOne(o => o.Customer).WithMany().IsRequired(false)` because the migration history once allowed orphans. The two diverge silently — `Order? Customer` is reference-typed and tolerated by the runtime, but a property test that generates `Order` graphs from `IModel` will produce `Order` instances with `Customer = null`. If your domain logic dereferences `order.Customer.Name` anywhere, the property fails on the first such draw.

This is not a bug in EF. It's a bug in the *contract* between the model configuration and the domain code. The strategy mechanically generates the legal value space the model claims; if your code can't handle that space, the test fails.

### 2. Value converter precision loss

`HasConversion<T, U>` lets you store `MoneyAmount` as `decimal`, `DateOnly` as a string, `Status` as an `int`. Each converter is a small function pair; each function pair is a place where round-tripping can lose information. The most common shape: a converter that truncates sub-second precision, drops trailing zeros, or normalises a string in only one direction.

`RoundtripAsserter.AssertRoundtripAsync` saves an entity, reads it back from a fresh `DbContext`, and walks scalar properties via `IProperty.PropertyInfo!.GetValue` to compare. The comparison reads the *post-converter* value, so an asymmetric converter shows up as a property-level diff: `expected '12:34:56.7890000', got '12:34:56.0000000'`. Without property testing, this surfaces in production when a downstream system compares timestamps and rejects the divergence.

### 3. Migration data loss on `Down`

Migrations apply forward in production and (usually) only forward. The `Down` direction is exercised in two scenarios: a developer rolling back during local iteration, and a deployment system reverting a failed release. Both are rare, both are high-stakes, both happen in environments where you can't easily re-derive the lost data.

`MigrationHarness.AssertUpDownIdempotentAsync` applies migrations to head, snapshots `sqlite_master`, runs `Down` on the latest, then re-applies `Up` and asserts the snapshot didn't drift. The harness catches three patterns:

- `Down` drops a table that `Up` only altered. Re-applying `Up` recreates the table from the model snapshot — and any data the developer expected `Down` to preserve is gone.
- `Down` forgets to drop an index `Up` created. The next `Up` collides on the index name.
- `Down` recreates a column under a slightly different definition. The schema text drifts; downstream queries that depended on the original definition silently break.

## Why `IModel` is enough metadata

EF Core's `IModel` carries every type-system constraint the framework itself enforces: CLR type, nullability, `MaxLength` for strings/blobs, `Precision`/`Scale` for decimals, primary keys, foreign keys, navigation cardinality. v1 of Conjecture.EFCore honours exactly that surface — and only that surface. `CheckConstraint` text, value-converter internals, and computed-column expressions are out of scope. The reason: provider-specific SQL fragments are unparseable without re-implementing each provider's parser, and the failure mode for an unhonoured `CheckConstraint` is loud (`DbUpdateException`) rather than silent. Loud failures are diagnosable; silent ones aren't.

ADR 0065 lists what's in and out for v1. The short version: type-system constraints in, behavioural constraints out, LINQ query-shape fuzzing deferred to a future `Conjecture.EFCore.Linq`.

## Bounded navigation depth, aligned with `Strategy.Recursive`

Cyclic navigations (`Customer ↔ Orders`) terminate at `maxDepth = 2` by default. The depth bound mirrors `Strategy.Recursive()` so users carry a single mental model across primitive recursion and entity-graph recursion. At the bound: required reference navigations reuse the first generated parent of the same target type; optional reference navigations are set to `null`; collection navigations are emitted empty. Owned types — entities with no independent identity — are generated inline regardless of depth, matching EF's own modelling semantics.

The depth bound exists for a practical reason: without it, generation diverges on any model with a cycle. With it, generation is deterministic and shrinks predictably. Users who want larger graphs can override per-build via `EntityStrategyBuilder.WithMaxDepth`.

## Composition with the Layer-1 interaction model

EF Core is the third transport concretion in the Conjecture interaction model — after HTTP (v0.19) and messaging (v0.21). The core of that model is `IInteractionTarget`, a single interface that resolves a named resource (HTTP host, message bus, or `DbContext`) and routes a generated `IInteraction` to it. `CompositeInteractionTarget` (introduced in ADR 0064) lets a single `InteractionStateMachine<TState>` interleave HTTP requests, message-queue publications, and database mutations against unified application state.

The implication: a property test for an ASP.NET Core endpoint that writes to a database can drive both — generating a sequence of HTTP `POST`s and asserting the resulting `DbContext` state matches a model. The state machine doesn't care that one step was HTTP and the next was a `SaveChanges`; the runner shrinks the entire interaction sequence as one trace. This composition is what unblocks the v0.26 (AspNetCore + EFCore) and v0.27 (Aspire + EFCore) integration packages.

## Why SQLite by default

The reference test provider in v1 is SQLite (in-memory). Three reasons:

- **It's real SQL.** Migrations apply, transactions commit, value converters round-trip through the wire format. A test that passes against SQLite is doing real database work, not pretending.
- **It's hermetic.** No external service, no Docker, no port allocation. Tests run in milliseconds and parallelise without coordination.
- **Its `IModel` shape is identical.** The model EF builds from `OnModelCreating` is provider-agnostic; the strategies the entity builder emits would be the same against PostgreSQL or SQL Server. Dropping to a different provider via `IDbTarget` is a one-line change.

The trade-off: SQLite has gaps in DDL (notably column rebuild) that real production providers don't. The integration self-test tier in `Conjecture.SelfTests/EFCore/` exercises the harness against a richer provider matrix; that tier is gated on a CI flag and is not the default test mode.

## What a passing property test buys you

A property test that runs `RoundtripAsserter.AssertRoundtripAsync` over `Strategy.Entity<T>(db)` for every aggregate root in your domain answers a hard question precisely: *for the entire space of structurally-valid entities the model claims, can I save and reload without observable change?* If the answer is yes, you've ruled out an entire class of value-converter, model-mismatch, and tracking-edge bugs in tens of seconds. If the answer is no, you have a shrunk counterexample — typically a single-field divergence — that goes straight into a regression test.

That ratio of cost to coverage is what property-based testing optimises for. EF Core's metadata-rich surface makes it one of the highest-leverage targets in the .NET ecosystem.

## See also

- [ADR 0065: Conjecture.EFCore package design](../../decisions/0065-conjecture-efcore-package-design.md)
- [Reference: Conjecture.EFCore](../reference/efcore.md)
- [Tutorial: Property tests for EF Core](../tutorials/10-efcore-integration.md)
- [Explanation: Property-based testing](property-based-testing.md)
- [Explanation: Shrinking](shrinking.md)
