# Conjecture.Messaging

Transport-agnostic messaging primitives for [Conjecture](https://github.com/kommundsen/Conjecture). Defines `MessageInteraction` (queue/topic, body, headers, message-id, correlation-id), `IMessageBusTarget` (publish, receive, ack, reject), and an in-memory implementation for unit tests. Adapter packages — [`Conjecture.Messaging.AzureServiceBus`](https://www.nuget.org/packages/Conjecture.Messaging.AzureServiceBus), [`Conjecture.Messaging.RabbitMq`](https://www.nuget.org/packages/Conjecture.Messaging.RabbitMq) — plug real brokers into the same model.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Messaging
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Messaging;

InMemoryMessageBusTarget bus = new();

Strategy<MessageInteraction> publishes = Strategy.Messaging
    .Publish("orders", Strategy.Arrays(Strategy.Integers<byte>(), 0, 256).Select(static b => (ReadOnlyMemory<byte>)b));

await bus.ExecuteAsync(publishes.Sample(), CancellationToken.None);

MessageInteraction? received = await bus.ReceiveAsync(
    "orders",
    timeout: TimeSpan.FromSeconds(1),
    CancellationToken.None);

if (received is not null)
{
    await bus.AcknowledgeAsync(received, CancellationToken.None);
}
```

Use `Strategy.Messaging.Consume(queue)` to model the receive side, and compose with `InteractionStateMachine<TState>` for stateful tests across publish/consume cycles.

## Types

| Type | Role |
|---|---|
| `MessageInteraction` | Readonly record: destination, body, headers, message id, correlation id. |
| `IMessageBusTarget` | Publish, receive, acknowledge, reject. |
| `InMemoryMessageBusTarget` | In-process implementation for unit tests. |
| `Strategy.Messaging.Publish(dest, body)` | Strategy of `MessageInteraction` representing a publish. |
| `Strategy.Messaging.Consume(dest)` | Strategy of `MessageInteraction` representing a consume. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)