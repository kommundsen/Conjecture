# Configure retry policy for Aspire property tests

Distributed tests are inherently flaky: containers may take extra milliseconds to become healthy after `StartAsync`, and health checks may transiently fail right after `ResetAsync`. `IAspireAppFixture` exposes a configurable retry policy so transient infrastructure noise does not surface as property failures.

## Default behaviour

The defaults are conservative:

| Property | Default | Meaning |
|---|---|---|
| `MaxRetryAttempts` | `3` | How many times to retry a failed `StartAsync` or health-check before giving up |
| `RetryDelay` | `500 ms` | Linear delay between attempts |
| `HealthCheckedResources` | `[]` (empty) | No health-check polling unless you opt in |

With the defaults, a transient container startup failure causes `StartAsync` to be retried up to three times at 500 ms intervals. If all three attempts fail, the exception propagates and the property is reported as failed.

## Override retry settings

Override the virtual properties on your fixture subclass:

```csharp
public class SlowContainerFixture : IAspireAppFixture
{
    // Allow more time for slow container environments (e.g., CI on shared runners)
    public override int MaxRetryAttempts => 5;
    public override TimeSpan RetryDelay => TimeSpan.FromSeconds(2);

    public override Task<DistributedApplication> StartAsync(CancellationToken ct = default)
    {
        // ...
    }

    public override Task ResetAsync(DistributedApplication app, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

## Enable health-check polling

By default, no health-check polling occurs. Add resource names to `HealthCheckedResources` to enable `WaitForHealthyAsync` after startup and after each `ResetAsync`:

```csharp
public class HealthAwareFixture : IAspireAppFixture
{
    public override IEnumerable<string> HealthCheckedResources =>
        ["store-api", "payment-service"];

    // ...
}
```

After `StartAsync` returns and after each `ResetAsync`, the runner calls `WaitForHealthyAsync(app, resourceName, ct)` for each listed resource before running the next example.

## Override `WaitForHealthyAsync`

The default `WaitForHealthyAsync` does nothing. Override it to use Aspire's built-in health-check API or a custom readiness check:

```csharp
public class HealthAwareFixture : IAspireAppFixture
{
    public override IEnumerable<string> HealthCheckedResources =>
        ["store-api", "payment-service"];

    public override async Task WaitForHealthyAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken ct = default)
    {
        await app.WaitForHealthyAsync(resourceName, cancellationToken: ct);
    }

    // ...
}
```

> [!NOTE]
> `WaitForHealthyAsync` is retried under the same `MaxRetryAttempts` / `RetryDelay` policy as `StartAsync`. If the resource does not become healthy within the retry budget, `DistributedApplicationHealthException` is thrown and the property fails.

## Opt out of health checks for a specific resource

Leave a resource out of `HealthCheckedResources`. The runner will not call `WaitForHealthyAsync` for resources not in the list.

```csharp
// Only health-check the API; skip the background worker (it has no health endpoint)
public override IEnumerable<string> HealthCheckedResources => ["store-api"];
```

## Retry exceptions

The retry policy applies to `HttpRequestException` and `IOException` thrown by `StartAsync`. Other exception types (e.g., `InvalidOperationException`) propagate immediately without retry — they indicate a configuration error, not a transient infrastructure failure.

## See also

- [Reset application state between examples](reset-aspire-state.md)
- [Reference: IAspireAppFixture](../reference/aspire.md)
- [Explanation: Why Aspire uses a shared lifecycle](../explanation/aspire-lifecycle.md)
