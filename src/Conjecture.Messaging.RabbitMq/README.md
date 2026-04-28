# Conjecture.Messaging.RabbitMq

RabbitMQ adapter for [Conjecture.Messaging](https://www.nuget.org/packages/Conjecture.Messaging). `RabbitMqTarget` implements `IMessageBusTarget` over the official `RabbitMQ.Client` library, so the same property-test bodies that run against the in-memory bus also run against a local broker, a Testcontainers instance, or production-like infrastructure.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Messaging
dotnet add package Conjecture.Messaging.RabbitMq
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Messaging.RabbitMq;

await using RabbitMqTarget target = await RabbitMqTarget.ConnectAsync(
    "amqp://guest:guest@localhost:5672/",
    CancellationToken.None);

Strategy<MessageInteraction> publishes = Generate.Messaging
    .Publish("orders", Generate.Bytes(0, 1024));

await target.ExecuteAsync(publishes.Sample(), CancellationToken.None);

MessageInteraction? message = await target.ReceiveAsync(
    "orders",
    timeout: TimeSpan.FromSeconds(5),
    CancellationToken.None);

if (message is not null)
{
    await target.AcknowledgeAsync(message, CancellationToken.None);
}
```

For unit tests, prefer `InMemoryMessageBusTarget` from `Conjecture.Messaging`; reserve this adapter for integration tests against a real broker.

## Types

| Type | Role |
|---|---|
| `RabbitMqTarget.ConnectAsync(connectionString, ct)` / `Connect(connectionString)` | Builds an `IMessageBusTarget` over `RabbitMQ.Client`. |
| `IRabbitMqConnectionAdapter` / `IRabbitMqChannelAdapter` / `IRabbitMqMessageAdapter` / `IRabbitMqReceivedMessageAdapter` | Seams for substituting fakes; the concrete adapter wraps `RabbitMQ.Client`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
