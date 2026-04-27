# Why composite HTTP+DB invariants find bugs

`Conjecture.AspNetCore` exercises ASP.NET Core endpoints under generated request loads. `Conjecture.EFCore` verifies entity roundtrip integrity, migration symmetry, and concurrency-token honesty. Each is a complete property-testing surface for its layer. Yet the most expensive bugs in real-world web apps live *between* the two layers — places where the HTTP response and the persisted state disagree about what actually happened. `Conjecture.AspNetCore.EFCore` exists to catch those.

## The bugs single-layer tests miss

### 1. Returns success while silently failing to flush

A handler calls `db.Add(order)`, then `await SomethingElseAsync()`, then returns `Created`. The `await SomethingElseAsync()` was supposed to be `await db.SaveChangesAsync()` — a typo. The test framework sees `201 Created` and is happy. The next request reading `/orders/{id}` returns 404, and the bug shows up in production telemetry as "occasional 404s after successful POST".

A property-test variant of `AssertRoundtripAsync` from `Conjecture.EFCore` would not catch this either: it operates on a `DbContext` directly, not on an HTTP endpoint. The HTTP layer's response and the DB's persisted state must be observed together.

### 2. Returns error while persisting partial state

A handler calls `db.Add(order)`, then validates a domain rule, then returns `400 Bad Request` *without* removing the entity from the change tracker. If anything downstream (a logging interceptor, a middleware, a sibling repository call) calls `SaveChanges`, the rejected order persists despite the error response. From the client's point of view the request failed; from the database's point of view it succeeded.

`AssertNoPartialWritesOnErrorAsync` snapshots the database immediately before and after the request and fails if a status code ≥ 400 left a non-empty diff. The minimal counterexample shrinks to the smallest payload that triggers the offending validation path — typically a single rule violation.

### 3. Cascades that drift from the model

EF Core's `OnDelete` configuration declares what should happen to dependent rows when a root is deleted: `Cascade`, `SetNull`, `Restrict`. The actual cascade is enforced by the underlying SQL engine (real provider) or emulated in memory (InMemory provider). When a future migration changes a relationship's `DeleteBehavior` without updating the application's invariant — or when a developer hand-writes a `DELETE` SQL fragment that bypasses EF — the configured behaviour and the observed behaviour diverge.

`AssertCascadeCorrectnessAsync` walks `IModel.GetEntityTypes()` and queries every required FK after a delete request. It asserts that surviving rows match the model's claim. The shrunk counterexample isolates the smallest dependent graph that contradicts the model — often a single root with one child row — which makes it easy to read off which relationship's `OnDelete` is wrong.

### 4. Idempotency claims that don't hold

REST contracts often advertise certain endpoints as idempotent: `PUT /orders/{id}`, `DELETE /orders/{id}`, `POST /upserts/orders`. Clients rely on this for retry logic, network resilience, and at-least-once message delivery. A handler that "almost" implements idempotency — perhaps it increments an audit counter on each call, or fails the second time because of a unique-constraint check it forgot to convert into an upsert — silently breaks the contract.

`AssertIdempotentAsync` is the simplest of the three invariants by construction: run the request twice; the database state after the second call must be observably identical to after the first; the status codes must match. The opt-in `MarkIdempotent` predicate avoids the false positives that HTTP-verb inference produces (POST upserts, PUT counter increments).

## What makes the composite shape work

The crucial design decision is that **both targets share one `IHost`**. `WebApplicationFactory<TEntryPoint>` hosts the real ASP.NET Core pipeline in process; `factory.Services.GetRequiredService<IHost>()` reaches into the same service container that the request pipeline uses. `HostHttpTarget` resolves an `HttpClient` against that host; `AspNetCoreDbTarget<TContext>` resolves the registered `DbContext` against the same host's `IServiceScopeFactory`. The before/after snapshots correlate with the HTTP response that triggered them because they observe the same in-process state, not a parallel database that drifts independently.

The second decision: **per-call DI scoping**. ASP.NET Core registers `DbContext` as `Scoped` — one instance per HTTP request. If `AspNetCoreDbTarget<TContext>` resolved the context once and reused it across examples, the change tracker would accumulate state, masking exactly the leak bugs the package is meant to find. Every `ResolveContext` / `Resolve` call creates a fresh `IServiceScope`, and the returned context's lifetime is bounded by the scope's. This makes leakage between examples impossible by construction.

The third decision: **reuse `EntitySnapshotter` from `Conjecture.EFCore`**. The snapshot/diff infrastructure is provider-agnostic and entity-graph-driven; it lives in `Conjecture.EFCore` (introduced by [#585](https://github.com/kommundsen/Conjecture/issues/585)) and the composite invariants consume it without re-implementing the walk. Three invariants share one well-tested helper, and the resulting failure messages have a uniform format across the EFCore stack.

## Composition with the Layer-1 interaction model

`AspNetCoreDbTarget<TContext>` implements `IDbTarget`, the same contract as `SqliteDbTarget` and `InMemoryDbTarget`. `IDbTarget` implements `IInteractionTarget`, the contract used by `CompositeInteractionTarget` to route a generated `IInteraction` (HTTP, gRPC, message-bus, or `DbInteraction`) to the right backend by `ResourceName`. A property test can therefore drive a single `InteractionStateMachine<TState>` that interleaves an HTTP `POST` and a database `SaveChanges` as one shrinking sequence — the invariants above are the convenience asserters built on top of that primitive.

This composition is the design pattern that unblocks the next satellite, [`Conjecture.Aspire.EFCore`](https://github.com/kommundsen/Conjecture/issues/553) (v0.27.0): a different fixture (the Aspire `DistributedApplication`), the same `IDbTarget` contract, the same composite invariants. The hosting model differs; the invariant shape does not.

## Why SQLite is the default

The reference test backing for cascade invariants is SQLite. Three reasons:

- **Cascades execute through real SQL.** The relational pipeline that PostgreSQL and SQL Server use is the same one SQLite uses. A passing `AssertCascadeCorrectnessAsync` against SQLite catches the bugs that drift between EF's declared model and the SQL the production provider would emit.
- **It's hermetic.** No external service, no Docker, no port allocation. Tests run in milliseconds and parallelise cleanly under a class fixture.
- **Provider-specific tier opts in.** The integration self-test tier exercises PostgreSQL behind `ASPNETCORE_EFCORE_INTEGRATION_TESTS=1`. Provider-specific divergences surface there without slowing the default unit tier.

The EF InMemory provider is documented as **not recommended for cascade invariants**. Its in-memory cascade emulation can drift from real SQL, so a passing assertion against InMemory does not guarantee correctness against a relational provider. Roundtrip and idempotency invariants are safe with InMemory; cascade is not.

## What a passing property test buys you

Three property tests — one per invariant — over the entire `DiscoveredEndpoint[]` of an ASP.NET Core API answer three precise questions in tens of seconds:

- For every endpoint that returns 4xx/5xx, is the database guaranteed unchanged?
- For every endpoint that deletes an aggregate root, do dependent rows match the model's declared `DeleteBehavior`?
- For every endpoint your contract claims is idempotent, does a replay actually produce identical state?

If those three properties hold, you have ruled out four of the highest-leverage bug classes in HTTP+DB applications. Failing properties shrink to the minimal payload + endpoint pair that triggers the divergence; the failing example goes straight into a regression test. That ratio of cost to coverage is what property-based testing optimises for.

## See also

- [Tutorial: Composite property tests for ASP.NET Core + EF Core](../tutorials/11-aspnetcore-efcore-integration.md)
- [Reference: Conjecture.AspNetCore.EFCore](../reference/aspnetcore-efcore.md)
- [Explanation: Why property testing finds EF Core bugs](efcore-property-testing.md)
- [Explanation: Why IHost + HttpClient for ASP.NET Core](aspnetcore-host-abstraction.md)
- [ADR 0066: Conjecture.AspNetCore.EFCore package design](../../decisions/0066-conjecture-aspnetcore-efcore-package-design.md)
