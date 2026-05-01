# How to generate Protobuf message payloads

Use `Strategy.FromProtobuf<T>()` or `Strategy.FromProtobuf(descriptor)` to produce `JsonElement` values shaped by a Protobuf message descriptor.

## Prerequisites

Install `Conjecture.Protobuf`:

```bash
dotnet add package Conjecture.Protobuf
```

Your project must already reference `Google.Protobuf` and have compiled `.proto` files (via `Grpc.Tools` or manual compilation).

## Generate from a compiled message type

Pass the message type as a generic parameter. Conjecture reads the `MessageDescriptor` via reflection:

```csharp
using System.Text.Json;
using Conjecture.Core;
using Conjecture.Protobuf;
using MyApp.Protos; // generated from your .proto file

Strategy<JsonElement> strategy = Strategy.FromProtobuf<CreateOrderRequest>();
```

Each generated `JsonElement` is a JSON object whose fields match the Protobuf message fields.

## Write the property test

# [xUnit v2](#tab/xunit-v2)

```csharp
using Conjecture.Core;
using Conjecture.Protobuf;
using Conjecture.Xunit;
using System.Text.Json;
using Xunit;
using MyApp.Protos;

public class OrderRequestTests
{
    [Property]
    public void CreateOrderRequest_GeneratesValidPayload(JsonElement payload)
    {
        Assert.Equal(JsonValueKind.Object, payload.ValueKind);
        // All required fields should be present
        Assert.True(payload.TryGetProperty("customerId", out _));
    }
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;
using Conjecture.Protobuf;
using Conjecture.Xunit.V3;
using System.Text.Json;
using Xunit;
using MyApp.Protos;

public class OrderRequestTests
{
    [Property]
    public void CreateOrderRequest_GeneratesValidPayload()
    {
        Strategy<JsonElement> strategy = Strategy.FromProtobuf<CreateOrderRequest>();
        foreach (JsonElement payload in DataGen.Sample(strategy, 50))
        {
            Assert.Equal(JsonValueKind.Object, payload.ValueKind);
        }
    }
}
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Core;
using Conjecture.Protobuf;
using Conjecture.NUnit;
using NUnit.Framework;
using System.Text.Json;
using MyApp.Protos;

[TestFixture]
public class OrderRequestTests
{
    [Property]
    public void CreateOrderRequest_GeneratesValidPayload()
    {
        Strategy<JsonElement> strategy = Strategy.FromProtobuf<CreateOrderRequest>();
        foreach (JsonElement payload in DataGen.Sample(strategy, 50))
        {
            Assert.That(payload.ValueKind, Is.EqualTo(JsonValueKind.Object));
        }
    }
}
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Core;
using Conjecture.Protobuf;
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using MyApp.Protos;

[TestClass]
public class OrderRequestTests
{
    [Property]
    public void CreateOrderRequest_GeneratesValidPayload()
    {
        Strategy<JsonElement> strategy = Strategy.FromProtobuf<CreateOrderRequest>();
        foreach (JsonElement payload in DataGen.Sample(strategy, 50))
        {
            Assert.AreEqual(JsonValueKind.Object, payload.ValueKind);
        }
    }
}
```

***

## Generate from a `MessageDescriptor` at runtime

When you don't have a compile-time type reference, pass a `MessageDescriptor` directly:

```csharp
using Google.Protobuf.Reflection;

MessageDescriptor descriptor = MyMessage.Descriptor;
Strategy<JsonElement> strategy = Strategy.FromProtobuf(descriptor);
```

## Control recursion depth

Protobuf messages can be self-referential (e.g., a `Node` message with a `Node child` field). Conjecture limits recursion to a configurable depth:

```csharp
Strategy<JsonElement> strategy = Strategy.FromProtobuf<TreeNode>(maxDepth: 3);
```

The default depth is 5. Recursive fields beyond the limit generate a null JSON value.

> [!NOTE]
> `oneof` fields generate exactly one arm per object. Repeated fields generate a JSON array of values.
