# Test messaging with the in-memory adapter

Use `InMemoryMessageBusTarget` to property-test consumer logic without bringing up a real broker. The in-memory adapter is fully deterministic and runs in milliseconds, which makes it the right choice for the bulk of your test suite — reserve real brokers for integration drift checks.

This how-to uses xUnit v3. The same shape works under any test runner that lets you `await` an async test method.

## Install

```xml
<PackageReference Include="Conjecture.Messaging" />
<PackageReference Include="Conjecture.Xunit.V3" />
```

## Round-trip a generated message

Given a consumer that uppercases message bodies, you want to assert that any byte body the publisher sends round-trips through the broker without corruption.

```csharp
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Xunit.V3;

public class OrderConsumerProperties
{
    [Property]
    public async Task PublishedBody_RoundTrips(CancellationToken ct)
    {
        InMemoryMessageBusTarget bus = new();

        Strategy<MessageInteraction> publishStrategy =
            Strategy.Messaging.Publish("orders", Strategy.Arrays<byte>(Strategy.Integers<byte>(), 0, 1024));

        await Property.ForAll(bus, publishStrategy, async (target, sent) =>
        {
            await target.ExecuteAsync(sent, ct);
            MessageInteraction? received = await target.ReceiveAsync("orders", TimeSpan.FromSeconds(1), ct);

            Assert.NotNull(received);
            Assert.Equal(sent.MessageId, received.MessageId);
            Assert.Equal(sent.Body.ToArray(), received.Body.ToArray());
            await target.AcknowledgeAsync(received, ct);
        }, ct);
    }
}
```

The strategy generates the body bytes; `MessageId` is generated deterministically by `Strategy.Messaging.Publish` (so failed runs reproduce byte-for-byte under the same seed).

## Test the redelivery path

`RejectAsync(message, requeue: true, ct)` puts a received message back on the queue. Use this to property-test consumer code that handles transient failures.

```csharp
[Property]
public async Task RejectedMessage_IsAvailableAgain(CancellationToken ct)
{
    InMemoryMessageBusTarget bus = new();
    Strategy<MessageInteraction> publishStrategy =
        Strategy.Messaging.Publish("retries", Strategy.Arrays<byte>(Strategy.Integers<byte>(), 0, 256));

    await Property.ForAll(bus, publishStrategy, async (target, sent) =>
    {
        await target.ExecuteAsync(sent, ct);

        MessageInteraction? first = await target.ReceiveAsync("retries", TimeSpan.FromSeconds(1), ct);
        Assert.NotNull(first);
        await target.RejectAsync(first, requeue: true, ct);

        MessageInteraction? second = await target.ReceiveAsync("retries", TimeSpan.FromSeconds(1), ct);
        Assert.NotNull(second);
        Assert.Equal(sent.Body.ToArray(), second.Body.ToArray());
        await target.AcknowledgeAsync(second, ct);
    }, ct);
}
```

`RejectAsync(..., requeue: false, ct)` discards the message — useful for testing dead-letter / poison-message handling.

## Compose body strategies with the schema generators

`Body` is `ReadOnlyMemory<byte>` so any byte-producing strategy composes:

```csharp
// Protobuf payloads — the byte strategy comes from Strategy.FromProtobuf<T>().
Strategy<MessageInteraction> protoMessages =
    Strategy.Messaging.Publish("events", Strategy.FromProtobuf<UserCreated>());

// JSON-Schema-driven bodies.
JsonDocument schema = JsonDocument.Parse(/* ... */);
Strategy<MessageInteraction> jsonMessages =
    Strategy.Messaging.Publish(
        "events",
        Strategy.FromJsonSchema(schema).Select(json => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json))));
```

## Cross-destination property tests

Two destinations on the same `InMemoryMessageBusTarget` are isolated — a publish on `"a"` is not visible from a receive on `"b"`. Use this to property-test fan-out / routing logic without a real exchange:

```csharp
[Property]
public async Task RoutesByDestination(CancellationToken ct)
{
    InMemoryMessageBusTarget bus = new();
    Strategy<MessageInteraction> aStrategy = Strategy.Messaging.Publish("a", Strategy.Arrays<byte>(Strategy.Integers<byte>(), 0, 64));
    Strategy<MessageInteraction> bStrategy = Strategy.Messaging.Publish("b", Strategy.Arrays<byte>(Strategy.Integers<byte>(), 0, 64));

    await Property.ForAll(bus, Strategy.Tuple(aStrategy, bStrategy), async (target, pair) =>
    {
        (MessageInteraction toA, MessageInteraction toB) = pair;
        await target.ExecuteAsync(toA, ct);
        await target.ExecuteAsync(toB, ct);

        MessageInteraction? fromA = await target.ReceiveAsync("a", TimeSpan.FromSeconds(1), ct);
        MessageInteraction? fromB = await target.ReceiveAsync("b", TimeSpan.FromSeconds(1), ct);

        Assert.Equal(toA.Body.ToArray(), fromA?.Body.ToArray());
        Assert.Equal(toB.Body.ToArray(), fromB?.Body.ToArray());
    }, ct);
}
```

## When to switch to a real broker

The in-memory adapter is the right home for ~95% of your messaging tests. Move to a real broker (see [Test messaging with Azure Service Bus](test-messaging-with-azure-service-bus.md) or [Test messaging with RabbitMQ](test-messaging-with-rabbitmq.md)) when you specifically need to validate that the SDK's wire-level behaviour matches what your adapter expects — header encoding, dead-letter routing, transaction semantics. Those tests live in your project's integration tier (gated on Docker availability).
