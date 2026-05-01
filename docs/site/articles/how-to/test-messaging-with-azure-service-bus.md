# Test messaging with Azure Service Bus

Use `AzureServiceBusTarget` when your production code talks to Azure Service Bus and you want to property-test the round-trip behaviour against the real SDK. For day-to-day adapter logic tests, prefer the [in-memory adapter](test-messaging-with-inmemory.md) — it's faster and deterministic. Reach for the real broker when you need to catch SDK drift.

This how-to uses xUnit v3.

## Install

```xml
<PackageReference Include="Conjecture.Messaging.AzureServiceBus" />
<PackageReference Include="Conjecture.Xunit.V3" />
```

## Connect to a broker

```csharp
using Conjecture.Messaging.AzureServiceBus;

AzureServiceBusTarget target = AzureServiceBusTarget.Connect(connectionString);
```

`Connect(connectionString)` builds a `ServiceBusClient`, wraps it in the adapter's internal seam, and returns a target you can hand straight to `Property.ForAll(...)`. If you need to inject your own `ServiceBusClient` (e.g. configured with custom retry policy, transport type, or DefaultAzureCredential), implement `IServiceBusClientAdapter` over your client and pass it to the `AzureServiceBusTarget(IServiceBusClientAdapter)` constructor.

## A round-trip property test

```csharp
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Messaging.AzureServiceBus;
using Conjecture.Xunit.V3;

public class OrdersQueueProperties : IAsyncLifetime
{
    private AzureServiceBusTarget target = null!;

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("CONJECTURE_ASB_CONNECTION_STRING")
                ?? throw new InvalidOperationException("Set CONJECTURE_ASB_CONNECTION_STRING for integration tests.");
        target = AzureServiceBusTarget.Connect(connectionString);
    }

    public async ValueTask DisposeAsync() => await target.DisposeAsync();

    [Property(Trait = "RequiresAzureServiceBus")]
    public async Task RoundTrip(CancellationToken ct)
    {
        Strategy<MessageInteraction> publishStrategy =
            Strategy.Messaging.Publish("orders", Strategy.Bytes(0, 1024));

        await Property.ForAll(target, publishStrategy, async (bus, sent) =>
        {
            await bus.ExecuteAsync(sent, ct);
            MessageInteraction? received = await bus.ReceiveAsync("orders", TimeSpan.FromSeconds(5), ct);
            Assert.NotNull(received);
            Assert.Equal(sent.MessageId, received.MessageId);
            Assert.Equal(sent.Body.ToArray(), received.Body.ToArray());
            await bus.AcknowledgeAsync(received, ct);
        }, ct);
    }
}
```

The class-scoped fixture creates the connection once and disposes it after every test in the class has run. That's deliberate — the cost of standing up a Service Bus connection (or container) dominates the cost of running individual tests, so amortizing it across the class is the right trade-off (see [ADR 0061](../decisions/0061-conjecture-messaging-package-design.md), "Per-adapter test strategy").

## Run against the Service Bus emulator (no Azure account needed)

The [Azure Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator) is a free, in-memory, Docker-hosted broker that speaks the real Service Bus wire protocol. It's the right target for most tests — it doesn't cost anything per message and runs locally.

```bash
docker run --pull always -d --name asb-emulator \
    -e ACCEPT_EULA=Y \
    -e SQL_SERVER=sql -e MSSQL_SA_PASSWORD=YourStrong!Passw0rd \
    -p 5672:5672 \
    mcr.microsoft.com/azure-messaging/servicebus-emulator:latest
```

Set `CONJECTURE_ASB_CONNECTION_STRING=Endpoint=sb://localhost;SharedAccessKeyName=...` and run your tests as above. The same property tests pass against both the emulator and a real Azure Service Bus namespace; the emulator just doesn't bill you for messages.

## Translation details to watch for

Headers are stored in `ServiceBusMessage.ApplicationProperties`, which is `IDictionary<string, object>`. The Conjecture contract is `IReadOnlyDictionary<string, string>`, so on receive any non-string value is converted via `Convert.ToString(value, CultureInfo.InvariantCulture)`. Strings round-trip exactly; numerics, booleans, and `DateTime` values come back as their invariant-culture string form.

`MessageId` round-trips exactly. `CorrelationId` round-trips exactly. `Body` round-trips byte-for-byte (the SDK exposes it as `BinaryData`, the adapter wraps it back into `ReadOnlyMemory<byte>`).

`RejectAsync(message, requeue: true, ct)` calls `AbandonMessageAsync` on the SDK receiver — the message becomes immediately available to the next receive on the same queue. `RejectAsync(message, requeue: false, ct)` calls `DeadLetterMessageAsync` — the message routes to the queue's dead-letter sub-queue. Use the latter to property-test poison-message handling.

## Testing without a broker at all

If you only want to test your *adapter wrapper* (e.g. a custom `IMessageBusTarget` that delegates to `AzureServiceBusTarget` and adds tracing), the seam interfaces (`IServiceBusClientAdapter`, `IServiceBusSender`, `IServiceBusReceiver`) let you substitute fakes without bringing up Service Bus at all. See `src/Conjecture.Messaging.AzureServiceBus.Tests/Fakes/` for the reference fake implementations the package's own tests use.
