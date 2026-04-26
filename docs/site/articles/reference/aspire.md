# Conjecture.Aspire reference

API reference for the `Conjecture.Aspire` package. Install via:

```xml
<PackageReference Include="Conjecture.Aspire" />
```

Framework adapter packages (`Conjecture.Aspire.Xunit`, `Conjecture.Aspire.NUnit`, `Conjecture.Aspire.MSTest`, `Conjecture.Aspire.TestingPlatform`) wire the runner into the `[Property]` attribute of each test framework.

---

## `IAspireAppFixture`

Abstract base class. Implement this to provide a `DistributedApplication` to the property runner. Despite the `I` prefix, this is an abstract class — C# default interface members are not accessible through concrete-typed variables, so the retry-policy defaults must live on an abstract class.

```csharp
namespace Conjecture.Aspire;

public abstract class IAspireAppFixture : IAsyncDisposable
```

### Members to override

#### `StartAsync(CancellationToken ct = default) → Task<DistributedApplication>`

Called **once per property run** before any examples execute. Build and start the `DistributedApplication` here using `DistributedApplicationTestingBuilder`.

```csharp
public override async Task<DistributedApplication> StartAsync(CancellationToken ct = default)
{
    DistributedApplicationTestingBuilder builder =
        await DistributedApplicationTestingBuilder.CreateAsync<Projects.MyAppHost>(ct);
    DistributedApplication app = await builder.BuildAsync(ct);
    await app.StartAsync(ct);
    return app;
}
```

The runner retries `StartAsync` up to `MaxRetryAttempts` times on `HttpRequestException` or `IOException`. All other exceptions propagate immediately.

#### `ResetAsync(DistributedApplication app, CancellationToken ct = default) → Task`

Called before each example **except the first**. Restore any state the previous example mutated without restarting containers.

```csharp
public override async Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
{
    using HttpClient client = app.CreateHttpClient("my-api");
    await client.DeleteAsync("/test/reset", ct);
}
```

#### `WaitForHealthyAsync(DistributedApplication app, string resourceName, CancellationToken ct = default) → Task`

Called after `StartAsync` and after each `ResetAsync` for every resource listed in `HealthCheckedResources`. Default implementation is a no-op. Override to poll Aspire's health endpoint:

```csharp
public override Task WaitForHealthyAsync(
    DistributedApplication app,
    string resourceName,
    CancellationToken ct = default)
    => app.WaitForHealthyAsync(resourceName, cancellationToken: ct);
```

### Virtual properties (override to tune)

| Property | Type | Default | Meaning |
|---|---|---|---|
| `MaxRetryAttempts` | `int` | `3` | Retry limit for `StartAsync` and `WaitForHealthyAsync` on transient failures |
| `RetryDelay` | `TimeSpan` | `500 ms` | Linear delay between retry attempts |
| `HealthCheckedResources` | `IEnumerable<string>` | `[]` | Resource names to health-check after startup and each reset |

### `DisposeAsync() → ValueTask`

Default implementation is a no-op. Override to dispose resources you manage directly.

---

## `AspireStateMachine<TState>`

Abstract base class. Extend this to define the commands, transitions, and invariants of your stateful property test against an Aspire-hosted application.

```csharp
namespace Conjecture.Aspire;

public abstract class AspireStateMachine<TState> : IStateMachine<TState, Interaction>
```

### Abstract members

#### `InitialState() → TState`

Returns the starting state before any commands execute.

#### `Commands(TState state) → IEnumerable<Strategy<Interaction>>`

Returns the set of `Interaction` strategies valid in the given state. The runner selects one strategy per command step using the current data stream. Return an empty sequence to halt the command sequence early.

#### `RunCommand(TState state, Interaction cmd) → TState`

Executes `cmd` against the running application and returns the next state. Called for each interaction in the generated sequence.

#### `Invariant(TState state) → void`

Asserts that the current state satisfies all invariants. Called after each `RunCommand`. Throw any exception to signal a property failure; the runner captures it and begins shrinking.

### Protected members

#### `GetClient(string resourceName) → HttpClient`

Returns an `HttpClient` pre-configured to reach the named Aspire resource. The name must match a resource defined in your AppHost project.

```csharp
using HttpClient client = GetClient("order-service");
HttpResponseMessage response = await client.PostAsJsonAsync("/orders", payload, ct);
```

Throws `InvalidOperationException` if called before `StartAsync` has been called by the runner.

---

## `Interaction`

Value type representing a single interaction with an Aspire-hosted resource.

```csharp
namespace Conjecture.Aspire;

public readonly record struct Interaction(
    string ResourceName,
    string Method,
    string Path,
    object? Body);
```

| Property | Type | Meaning |
|---|---|---|
| `ResourceName` | `string` | Aspire resource name (passed to `GetClient` or `app.CreateHttpClient`) |
| `Method` | `string` | HTTP verb (`"GET"`, `"POST"`, `"DELETE"`, etc.) or message-bus verb (`"PUBLISH"`) |
| `Path` | `string` | URL path for HTTP interactions; queue/topic name for messaging interactions |
| `Body` | `object?` | Payload to serialise and send; `null` for requests with no body |

`Interaction` is a value type with structural equality. Two `Interaction` values are equal when all four properties are equal.

---

## Failure report format

When a property fails, Conjecture emits a shrunk failure report:

```text
Falsified after <N> examples (<M> commands total).
Shrunk to <K> commands:

  <METHOD> <PATH> [body: ...]  (resource: <ResourceName>)
    → state: <TState.ToString()>
  ...
  <last command>
    → <ExceptionType>: <Message>

Service logs (<ResourceName>):
  [HH:mm:ss] ...
```

- **Shrunk commands** — the minimal interaction sequence that reproduces the failure.
- **State transitions** — the model state after each command.
- **Service logs** — stdout/stderr captured from each Aspire resource involved in the failing trace.

To reproduce: pass `[ConjectureSettings(Seed = <seed>)]` on the test method. The seed appears in the first line of the failure output.

---

## See also

- [Tutorial: Property tests for Aspire apps](../tutorials/09-aspire-integration.md)
- [How-to: Reset application state between examples](reset-aspire-state.md)
- [How-to: Configure retry policy](configure-aspire-retry.md)
- [Explanation: Why Aspire uses a shared lifecycle](../explanation/aspire-lifecycle.md)
- [ADR 0064: Conjecture.Aspire package design](../../decisions/0064-conjecture-aspire-package-design.md)
