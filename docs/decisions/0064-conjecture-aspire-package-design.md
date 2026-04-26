# 0064. Conjecture.Aspire package design

**Date:** 2026-04-26
**Status:** Accepted

## Context

ADR 0059 ships `Conjecture.Interactions` plus `Conjecture.Http` as the v0.20 interaction foundation. ADR 0061 (`Conjecture.Messaging`), ADR 0062 (`Conjecture.Grpc`), and ADR 0063 (`Conjecture.AspNetCore`) extend that foundation across the major transport and host layers, validating that the interaction model generalises beyond a single service.

Modern .NET applications are increasingly distributed systems — microservices communicating over HTTP, gRPC, and message queues, orchestrated by .NET Aspire. Testing these systems for correctness is hard: individual unit tests pass, but emergent behavior across services breaks under specific interaction patterns. Property-based testing can systematically explore these interaction spaces, and Aspire's orchestration model provides the infrastructure to spin up, configure, and tear down multi-service environments for each test run.

.NET Aspire provides a programmatic app model (`IDistributedApplicationBuilder`) that defines services, dependencies, and connections as code. Aspire's `DistributedApplicationTestingBuilder` already supports integration testing by spinning up the full app model in-process. Conjecture can generate request sequences, message payloads, and timing variations, then shrink failing interaction traces to minimal reproductions — all within Aspire's managed lifecycle.

The decision must answer:

- Ship as a separate `Conjecture.Aspire` package or integrate into existing test adapters?
- How is the Aspire app lifecycle managed per property run — isolated per example, or shared?
- Should interaction strategies be built on `IStateMachine<TState, Interaction>` or a new abstraction?
- How are flaky distributed tests (network timeouts, container startup race conditions) handled?
- How is service discovery modelled — dynamic ports, named resources?
- When and how are health checks run before each example?
- What is included in failure reports?

## Decision

Ship a single `Conjecture.Aspire` NuGet package containing `IAspireAppFixture`, `AspireStateMachine`, `AspirePropertyRunner`, and the `Generate.Interactions` extension block for Aspire-hosted services. The package depends on `Aspire.Hosting.Testing` and `Conjecture.Core`; it does not reference any test framework directly — framework wiring is handled by thin satellite packages per ADR 0055.

### Package topology — separate satellite, not embedded

`Conjecture.Aspire` is a standalone satellite package, not embedded into `Conjecture.Xunit`, `Conjecture.NUnit`, or any other test adapter. The dependency on `Aspire.Hosting.Testing` (and transitively on `Aspire.Hosting`) is significant and should not be forced onto consumers who do not use Aspire. Framework adapters reference `Conjecture.Aspire` optionally via satellite packages (`Conjecture.Aspire.Xunit`, etc.) per ADR 0055.

### App lifecycle — shared per property run via `IAspireAppFixture`

Spinning up a full Aspire app model (container startup, readiness checks, port allocation) takes several seconds and is not viable per example. `IAspireAppFixture` encapsulates a `DistributedApplication` that is started **once per property run** and reset (not restarted) between examples via an `IExampleSetup` hook. Framework-level setup (xUnit class fixture, NUnit `[OneTimeSetUp]`, MSTest `[ClassInitialize]`) manages the fixture lifetime.

```csharp
public interface IAspireAppFixture : IAsyncDisposable
{
    DistributedApplication App { get; }
    HttpClient CreateHttpClient(string resourceName);
    Task ResetAsync(CancellationToken ct = default);
    AspireFixtureOptions Options { get; }
}

public sealed record AspireFixtureOptions
{
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);
    public IReadOnlyList<string> HealthCheckedResources { get; init; } = [];
}
```

`ResetAsync` is called by the runner before each example. Its implementation is user-supplied (via a delegate on the builder), allowing callers to roll back database state, purge queue messages, or reset in-memory state without restarting containers.

Per-example isolation via individual `DistributedApplication` instances is explicitly out of scope for v1; it remains an option for future exploration via a `[PropertyIsolation(Isolation.PerExample)]` attribute.

### Interaction strategies — `IStateMachine<TState, Interaction>` reuse

Interaction strategies are built on the existing `IStateMachine<TState, Interaction>` abstraction (ADR 0015, ADR 0033) to directly reuse `CommandSequenceShrinkPass` (ADR 0034). This is preferred over a new abstraction because:

- Shrinking of multi-step interaction sequences is already solved for stateful tests.
- The `Interaction` type from `Conjecture.Interactions` (ADR 0059) is the command type — no parallel command hierarchy is needed.
- Property tests that exercise state transitions across services map naturally to the state machine model.

`AspireStateMachine<TState>` is a convenience base class that implements `IStateMachine<TState, Interaction>` with Aspire-specific plumbing (`IAspireAppFixture` injection, health-check gating), but advanced users can implement `IStateMachine<TState, Interaction>` directly.

### Retry policy — configurable on `AspireFixtureOptions`

Distributed tests are inherently flaky: containers may be slow to become healthy, message queues may take extra milliseconds to deliver, and health checks may transiently fail immediately after `StartAsync` returns. `AspireFixtureOptions.MaxRetryAttempts` and `RetryDelay` configure a simple linear retry around each example execution. The retry is applied by `AspirePropertyRunner` and is invisible to the property assertion — a retried example that eventually passes is counted as a pass; a retried example that exhausts attempts is counted as a failure and shrunk normally.

Retry is not a correctness escape hatch: if an invariant is genuinely violated on every retry attempt, the failure is reported and shrunk. Retry only masks transient infrastructure noise, not true property failures.

### Service discovery — named resource strings resolved at execution time

Service discovery is handled via Aspire's built-in resource resolution: `IAspireAppFixture.CreateHttpClient(resourceName)` delegates to `DistributedApplication.CreateHttpClient(resourceName)`, which resolves the dynamic port and baseAddress allocated at startup. Named resource strings are the canonical Aspire model — no static port configuration, no DNS injection.

gRPC channels, message queue clients, and database connections follow the same pattern: callers resolve them from `fixture.App.Services` or via `fixture.App.GetConnectionString(resourceName)`. The package does not provide a separate client factory — Aspire already has one.

### Health checks — `WaitForHealthyAsync` before each example, opt-out via `HealthCheckedResources`

Before each example execution, `AspirePropertyRunner` calls `DistributedApplicationExtensions.WaitForHealthyAsync` for the resources listed in `AspireFixtureOptions.HealthCheckedResources`. An empty list (the default) skips health-check polling entirely — apps without health endpoints use `ResetAsync` for readiness instead.

`WaitForHealthyAsync` is bounded by `MaxRetryAttempts` and `RetryDelay`. A resource that does not become healthy within the retry budget causes the example to fail with a `DistributedApplicationHealthException`, not a property failure. This surfaces container startup failures without polluting the shrunk trace with unrelated interaction steps.

### Failure reports — shrunk interaction trace + service logs

When a property fails, the failure report includes:

1. The shrunk `Interaction[]` sequence — the minimal list of cross-service calls that reproduces the failure.
2. Captured service logs for each resource involved in the failing trace, pulled from `DistributedApplication`'s output-watching API.

Dashboard links (Aspire Dashboard, OpenTelemetry traces) are deferred to a future release. The v1 failure report provides enough information to reproduce the failure locally (`dotnet test --seed <N>`) and to understand which service emitted the relevant log lines.

The shrunk trace and logs are emitted via `IFailureReporter` (ADR 0022) so they appear in the test framework's standard output and in the Conjecture failure database.

### Built-in strategies in v1

Three categories of built-in strategy extensions ship in v1:

- **HTTP endpoint interactions** — `Generate.HttpInteractions(fixture.CreateHttpClient("svc"))` produces `Strategy<HttpInteraction>` for known endpoints. Composes with `Conjecture.AspNetCore` for endpoint-discovery-driven synthesis.
- **Message queue payloads** — `Generate.MessageInteractions<TMessage>(fixture.App.GetConnectionString("queue"))` produces `Strategy<MessageInteraction>` via the existing `Conjecture.Messaging` shape. Transport specifics (Azure Service Bus, RabbitMQ) are handled by the existing transport packages — the Aspire integration resolves connection strings; it does not re-implement messaging.
- **Database state seeding** — `Generate.EntitySet<T>()` (sub-issue #439) produces `Strategy<IReadOnlyList<T>>` for seeding database state before or during an interaction sequence. Used inside `ResetAsync` to populate a fresh entity set per example.

### Per-adapter test strategy

Two tiers, mirroring ADR 0061 and ADR 0063:

- **Unit tier** (`Conjecture.Aspire.Tests`) — fake `IAspireAppFixture` with an in-memory `DistributedApplication` substitute. Covers fixture lifecycle, health-check gating, retry logic, failure-report assembly, and shrunk-trace emission. No real containers.
- **Integration tier** (`Conjecture.SelfTests/Aspire/`) — real `DistributedApplicationTestingBuilder` against a minimal two-service AppHost (HTTP service + message queue consumer). Asserts invariants across services, verifies shrinking produces a minimal multi-step trace, and checks service logs appear in failure output. Gated behind `ASPIRE_INTEGRATION_TESTS=1` because container startup requires Docker; off by default in CI.

## Consequences

**Easier:**

- `IStateMachine<TState, Interaction>` + `CommandSequenceShrinkPass` reuse means multi-service trace shrinking works immediately, without new shrink infrastructure.
- Shared-per-run lifecycle amortises container startup across the full property run; examples are fast after the first setup.
- Named-resource service discovery follows Aspire conventions exactly — no mapping layer, no static config.
- Satellite package topology keeps `Aspire.Hosting.Testing` out of consumers who do not use Aspire.
- Failure reports include service logs by default, giving immediate context without requiring users to attach a debugger or replay against Aspire Dashboard.

**Harder:**

- Shared lifecycle means example isolation is the user's responsibility via `ResetAsync`. Forgetting to reset state is a common source of test flakiness; documentation and the `[Property]` runner tooling (ADR 0037) will surface this via the Conjecture observability hooks when state-dependent properties pass inconsistently across runs.
- `WaitForHealthyAsync` adds latency to the first example after each reset. This is unavoidable for containerised dependencies; retry parameters must be tuned per environment (local vs CI).
- v1 does not support per-example isolation. Teams with truly stateful services that cannot be cheaply reset will be blocked until per-example isolation ships in v2.
- The integration self-test tier requires Docker and is off by default in CI, which means Aspire-specific regressions may not be caught until a contributor enables the flag.

## Alternatives Considered

**Integrate into existing test adapters** — embed Aspire support in `Conjecture.Xunit`, `Conjecture.NUnit`, etc. Rejected: drags `Aspire.Hosting.Testing` into every test adapter regardless of whether the user uses Aspire. The satellite package pattern (ADR 0055) is the established model and is followed here.

**Per-example `DistributedApplication` instances** — start and stop the full app model for each generated example. Rejected for v1: startup cost is measured in seconds per run; a property with 100 examples would take minutes. May be reconsidered as a `[PropertyIsolation(Isolation.PerExample)]` opt-in in v2 if Aspire reduces startup cost significantly.

**New `IInteractionStateMachine` abstraction** — introduce a separate interface independent of `IStateMachine<TState, Interaction>`. Rejected: duplicates the state machine contract, loses `CommandSequenceShrinkPass` reuse, and adds a parallel type hierarchy that consumers must learn. The existing `IStateMachine<TState, Interaction>` is sufficient.

**No retry policy — fail immediately on transient errors** — let users configure retries externally. Rejected: distributed test infrastructure flakiness is universal enough that forcing every consumer to wire their own retry harness creates unnecessary friction. The configurable retry is simple (linear backoff, bounded attempts) and invisible when not needed (default `MaxRetryAttempts = 3` covers the common case).

**Pull service logs on demand only** — do not capture logs automatically; require users to call `fixture.App.GetLogs(resourceName)` in assertions. Rejected: missed failures are the most expensive outcome in property testing. Capturing logs automatically on failure costs nothing in the passing case (no logs are pulled) and is invaluable in the failing case. Users who do not want verbose output can suppress via `IFailureReporter` configuration.

**Include Aspire Dashboard trace links in v1** — emit OTLP trace IDs and dashboard URLs alongside shrunk traces. Rejected: requires the dashboard to be running (not guaranteed in CI), requires stable trace correlation between shrunk examples and their original runs, and the log-based report is sufficient for local reproduction. Deferred to v2 when OpenTelemetry integration is more mature.
