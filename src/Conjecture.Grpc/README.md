# Conjecture.Grpc

gRPC interaction primitives for [Conjecture](https://github.com/kommundsen/Conjecture). Defines `GrpcInteraction` (a serializable description of a unary, client-stream, server-stream, or bidirectional gRPC call), `IGrpcTarget` (resolves a `CallInvoker`), and `GenerateGrpc` (strategies for each RPC mode), so a property test can drive any gRPC method with random inputs and shrink failures.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Grpc
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Grpc;
using Conjecture.Xunit;
using Grpc.Core;

public class OrderingServiceTests
{
    [Property]
    public async Task UnaryCall_NeverPanics(GrpcInteraction interaction)
    {
        Strategy<PlaceOrderRequest> requests = Strategy.Just(new PlaceOrderRequest { Quantity = 1 });
        Strategy<GrpcInteraction> calls = GenerateGrpc.Unary(
            "ordering",
            OrderingService.PlaceOrderMethod,
            requests);

        GrpcChannelTarget target = new("ordering", "https://localhost:5001");
        GrpcResponse response = (GrpcResponse)(await target.ExecuteAsync(calls.Sample(), default))!;

        response.AssertNoUnknownStatus();
    }
}
```

Use `HostGrpcTarget` to dispatch in-process against an `IHost` (e.g. when testing alongside ASP.NET Core's `WebApplicationFactory`).

## Types

| Type | Role |
|---|---|
| `GrpcInteraction` | Readonly record describing a single gRPC call. |
| `GrpcResponse` | Status, response messages, headers, trailers. |
| `GrpcRpcMode` | `Unary`, `ServerStream`, `ClientStream`, `Bidi`. |
| `IGrpcTarget` | Resolves a `CallInvoker` for a named resource. |
| `GrpcChannelTarget` | `IGrpcTarget` over a `GrpcChannel`. |
| `HostGrpcTarget` | `IGrpcTarget` over an `IHost`. |
| `GenerateGrpc.Unary` / `ServerStream` / `ClientStream` / `BidiStream` | Strategies per RPC mode. |
| `GrpcInvariantExtensions` | `AssertStatusOk`, `AssertStatus`, `AssertNoUnknownStatus`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)