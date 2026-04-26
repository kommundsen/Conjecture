# Why Aspire uses a shared app lifecycle

`Conjecture.Aspire` starts the `DistributedApplication` once per property run and reuses it across all examples. This page explains why — and what the trade-offs are.

## The cost of container startup

Starting an Aspire app model means pulling and starting one or more container images, waiting for TCP sockets to open, running database migrations, and waiting for health checks to pass. On a developer machine with warm images this takes 5–20 seconds. In CI, cold images can push that to 60+ seconds.

A property test generates 100 examples by default. If the app were restarted between every example, a single property would take 8–100 minutes. That makes property testing impractical for distributed systems.

Shared lifecycle reduces startup cost to a one-time fixed overhead. Examples run in tens of milliseconds each — the same order of magnitude as HTTP integration tests.

## What "shared" means in practice

The `DistributedApplication` is started once at the beginning of `Property.ForAspire(...)`. Between examples, `IAspireAppFixture.ResetAsync` runs to restore observable state:

```
StartAsync()
  ↓
[Example 1]
ResetAsync()
  ↓
[Example 2]
ResetAsync()
  ↓
...
  ↓
[Example N]
DisposeAsync()
```

`ResetAsync` is your responsibility — the runner does not know whether your app uses a SQL database, a message queue, an in-memory cache, or all three. You write the reset logic once, and the runner calls it before every example after the first.

## Why `IStateMachine` for interactions

`AspireStateMachine<TState>` is built on the existing `IStateMachine<TState, Interaction>` abstraction. This is not accidental — it directly reuses `CommandSequenceShrinkPass`, the shrinker that already knows how to minimise multi-step state machine sequences.

When a property fails on a sequence of 20 cross-service interactions, the shrinker tries removing each interaction independently, then tries reducing payloads, then tries simplifying the model state. None of this logic is Aspire-specific. It lives in `Conjecture.Core` and was built for stateful testing (ADR 0034). Aspire integration inherits it for free.

The alternative — a new `IAspireInteraction` abstraction with its own shrinker — would have produced an equally capable shrinker at the cost of duplicating several hundred lines of shrinking infrastructure. Reusing `IStateMachine` means the Aspire shrinker benefits from every improvement made to the core stateful-testing machinery.

## Why `Interaction` is a value type

`Interaction` is a `readonly record struct`. The shrinker records, replays, and compares thousands of interaction sequences during shrinking. Value-type equality makes sequence comparison O(N) with no heap allocation per comparison. For a 20-step sequence shrunk across 500 candidates, this removes ~10,000 heap allocations from the shrink budget.

## What `WaitForHealthyAsync` does

After `StartAsync` returns and after each `ResetAsync`, the runner calls `WaitForHealthyAsync` for each resource listed in `HealthCheckedResources`. The default implementation is a no-op.

The hook exists because some services are not immediately ready after `StartAsync` returns. A database server may accept TCP connections before finishing its migration. A message consumer may start before its queue bindings are registered. Without health-check polling, the first example in a run (or the first example after a `ResetAsync` that flushes a queue) may hit a transient "not ready" failure that the runner would retry — burning retry budget on infrastructure noise instead of property noise.

Opt in by listing resource names:

```csharp
public override IEnumerable<string> HealthCheckedResources => ["order-api", "postgres"];
```

And override `WaitForHealthyAsync` to use Aspire's built-in API:

```csharp
public override Task WaitForHealthyAsync(
    DistributedApplication app,
    string resourceName,
    CancellationToken ct = default)
    => app.WaitForHealthyAsync(resourceName, cancellationToken: ct);
```

Resources not in `HealthCheckedResources` receive no health-check polling. Services that become ready well before their first interaction (typical for in-memory fakes) do not need to be listed.

## Trade-offs

**Easier:** Container startup cost is a one-time fixed overhead. Examples run at integration-test speed. The same `IStateMachine`/`CommandSequenceShrinkPass` machinery handles shrinking without any Aspire-specific code.

**Harder:** Example isolation is your responsibility. Forgetting to reset a table or flush a queue means example N can corrupt example N+1's initial state, producing failures that depend on example order and are hard to reproduce from a seed alone.

**The gap:** Per-example isolation — starting and stopping a fresh `DistributedApplication` for every example — is not supported in v1. For services with complex state that cannot be cheaply reset, this is a real limitation. It may ship as a `[PropertyIsolation(Isolation.PerExample)]` opt-in in a future release if Aspire reduces per-instance startup cost.

## Why the package is separate

`Conjecture.Aspire` depends on `Aspire.Hosting.Testing`, which transitively pulls in `Aspire.Hosting` and the full Microsoft.Extensions.Hosting stack. That dependency should not be forced on consumers who do not use Aspire. Shipping as a standalone satellite package (following ADR 0055) keeps `Conjecture.Xunit`, `Conjecture.NUnit`, etc. free of the dependency.

Framework adapters (`Conjecture.Aspire.Xunit`, `Conjecture.Aspire.NUnit`, `Conjecture.Aspire.MSTest`, `Conjecture.Aspire.TestingPlatform`) are thin wrappers that reference both the framework package and `Conjecture.Aspire`. A project that uses xUnit and Aspire adds `Conjecture.Aspire.Xunit` and gets both the `[Property]` attribute and the Aspire runner.

For the full design rationale see [ADR 0064](../../decisions/0064-conjecture-aspire-package-design.md).
