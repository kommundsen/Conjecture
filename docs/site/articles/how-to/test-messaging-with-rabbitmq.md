# Test messaging with RabbitMQ

Use `RabbitMqTarget` when your production code talks to RabbitMQ and you want to property-test the round-trip behaviour against the real `RabbitMQ.Client` SDK. For day-to-day adapter logic tests, prefer the [in-memory adapter](test-messaging-with-inmemory.md) — it's faster and deterministic. Reach for the real broker when you need to catch SDK drift.

This how-to uses xUnit v3.

## Install

```xml
<PackageReference Include="Conjecture.Messaging.RabbitMq" />
<PackageReference Include="Conjecture.Xunit.V3" />
```

## Connect to a broker

```csharp
using Conjecture.Messaging.RabbitMq;

// Synchronous (blocks the thread on connection setup):
RabbitMqTarget target = RabbitMqTarget.Connect("amqp://guest:guest@localhost:5672");

// Asynchronous (preferred — RabbitMQ.Client 7.x is async-only):
RabbitMqTarget target = await RabbitMqTarget.ConnectAsync("amqp://guest:guest@localhost:5672", cancellationToken);
```

`ConnectAsync` is preferred — `RabbitMQ.Client` 7.x is async throughout, and the synchronous `Connect` blocks the calling thread on connection establishment. Use the sync overload only in fixture code where there's no `CancellationToken` to honour.

## A round-trip property test

```csharp
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Messaging.RabbitMq;
using Conjecture.Xunit.V3;

public class OrdersQueueProperties : IAsyncLifetime
{
    private RabbitMqTarget target = null!;

    public async ValueTask InitializeAsync()
    {
        string connectionString =
            Environment.GetEnvironmentVariable("CONJECTURE_RABBITMQ_CONNECTION_STRING")
                ?? "amqp://guest:guest@localhost:5672";
        target = await RabbitMqTarget.ConnectAsync(connectionString, CancellationToken.None);
    }

    public async ValueTask DisposeAsync() => await target.DisposeAsync();

    [Property(Trait = "RequiresRabbitMq")]
    public async Task RoundTrip(CancellationToken ct)
    {
        Strategy<MessageInteraction> publishStrategy =
            Generate.Messaging.Publish("orders", Generate.Bytes(0, 1024));

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

The class-scoped fixture amortizes the cost of bringing up the connection across every test in the class — see [ADR 0061](../decisions/0061-conjecture-messaging-package-design.md) ("Per-adapter test strategy") for the rationale.

## Run against a local container

The official RabbitMQ Docker image is the simplest way to get a broker for tests:

```bash
docker run -d --name rabbitmq \
    -p 5672:5672 -p 15672:15672 \
    rabbitmq:3-management
```

The default user is `guest` / `guest`, the AMQP endpoint is `amqp://guest:guest@localhost:5672`, and the management UI is at http://localhost:15672. Tests using the connection string above will pick this up automatically.

## Translation details to watch for

RabbitMQ stores `IBasicProperties.Headers` values as `byte[]` for strings (the AMQP wire format encodes them as long-strings). The Conjecture contract is `IReadOnlyDictionary<string, string>` so the adapter UTF-8 decodes byte-array values on receive. Non-byte-array, non-null values come back via `Convert.ToString(value, CultureInfo.InvariantCulture)` rather than being silently dropped.

`MessageId`, `CorrelationId`, and `Body` round-trip exactly. The adapter publishes to the default exchange with the destination as the routing key, so a queue named `"orders"` receives messages routed to `"orders"`. If you need topic-style routing, declare the exchange and bindings out-of-band and put the routing key in your `Headers` (`x-routing-key` is a common convention) — the adapter doesn't try to abstract that for you.

`RejectAsync(message, requeue: true, ct)` calls `BasicNackAsync(deliveryTag, multiple: false, requeue: true)` — the message becomes immediately available to the next receive on the same queue. `RejectAsync(message, requeue: false, ct)` calls `BasicNackAsync(deliveryTag, multiple: false, requeue: false)` — the message is discarded (or routed to the dead-letter exchange if the queue has one configured). Use the latter to property-test poison-message handling.

## A note on the sync seam

The current adapter exposes a synchronous seam (`IRabbitMqChannelAdapter.BasicPublish` returns `void`, etc.) that bridges to the async `RabbitMQ.Client` 7.x SDK via `.GetAwaiter().GetResult()`. This is a known limitation tracked in a follow-up sub-issue and will become an async seam in a future release. For property-testing workloads it's functionally correct; for high-throughput production paths use the SDK directly until the async seam ships.

## Testing without a broker at all

If you only want to test your *adapter wrapper* (e.g. a custom `IMessageBusTarget` that delegates to `RabbitMqTarget`), the seam interfaces (`IRabbitMqConnectionAdapter`, `IRabbitMqChannelAdapter`, `IRabbitMqReceivedMessageAdapter`) let you substitute fakes without bringing up RabbitMQ at all. See `src/Conjecture.Messaging.RabbitMq.Tests/Fakes/` for the reference fake implementations the package's own tests use.
