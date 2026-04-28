# Conjecture.Messaging.AzureServiceBus

Azure Service Bus adapter for [Conjecture.Messaging](https://www.nuget.org/packages/Conjecture.Messaging). `AzureServiceBusTarget` implements `IMessageBusTarget` over the official `Azure.Messaging.ServiceBus` client, so the same property-test bodies that run against the in-memory bus also run against a real namespace (or the [Service Bus emulator](https://learn.microsoft.com/azure/service-bus-messaging/test-locally-with-service-bus-emulator)).

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Messaging
dotnet add package Conjecture.Messaging.AzureServiceBus
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Messaging;
using Conjecture.Messaging.AzureServiceBus;

await using AzureServiceBusTarget target = AzureServiceBusTarget.Connect(
    "Endpoint=sb://my.servicebus.windows.net/;SharedAccessKeyName=…;SharedAccessKey=…");

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

For unit tests, prefer `InMemoryMessageBusTarget` from `Conjecture.Messaging`; reserve this adapter for integration / SelfTests against the emulator or a real namespace.

## Types

| Type | Role |
|---|---|
| `AzureServiceBusTarget.Connect(connectionString)` | Builds an `IMessageBusTarget` over `ServiceBusClient`. |
| `IServiceBusClientAdapter` / `IServiceBusSender` / `IServiceBusReceiver` / `IServiceBusMessageAdapter` / `IServiceBusReceivedMessageAdapter` | Seams for substituting fakes; the concrete adapter wraps `Azure.Messaging.ServiceBus`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
