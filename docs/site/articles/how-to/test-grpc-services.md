# Property-test a gRPC service

Use `Conjecture.Grpc` to drive unary and streaming gRPC calls under a property runner. The strategies generate request payloads, the targets dispatch via `Grpc.Net.Client`, and `GrpcResponse.AssertStatus*` extensions catch invariant violations.

This how-to uses xUnit v3. The same shape works under any test runner that lets you `await` an async test method — see [Per-runner adapters](#per-runner-adapters) at the bottom for the package + import differences for xUnit v2, NUnit, MSTest, TestingPlatform, Expecto, Interactive, and LinqPad.

## Install

```xml
<PackageReference Include="Conjecture.Grpc" />
<PackageReference Include="Conjecture.Xunit.V3" />
<PackageReference Include="Conjecture.Protobuf" />        <!-- for Strategy.FromProtobuf<T>() -->
<PackageReference Include="Grpc.AspNetCore" />            <!-- when using HostGrpcTarget -->
<PackageReference Include="Microsoft.AspNetCore.TestHost" /><!-- ditto -->
```

Generate your service stubs the usual way (`Grpc.Tools` `<Protobuf Include="..." />`).

## Test a unary call against an in-process host

`HostGrpcTarget` wraps an `IHost` running your gRPC services and routes calls through `TestServer.CreateHandler()` — no socket binding, no port assignment. Build the host once per test class.

```csharp
using Conjecture.Core;
using Conjecture.Grpc;
using Conjecture.Protobuf;
using Conjecture.Xunit.V3;

using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class GreeterProperties : IAsyncLifetime
{
    private IHost host = null!;
    private HostGrpcTarget target = null!;

    public async ValueTask InitializeAsync()
    {
        host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(s => s.AddGrpc())
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(e => e.MapGrpcService<GreeterService>());
                }))
            .StartAsync();
        target = new HostGrpcTarget("greeter", host);
    }

    public async ValueTask DisposeAsync()
    {
        await target.DisposeAsync();
        host.Dispose();
    }

    [Property]
    public async Task SayHello_ReturnsOk(CancellationToken ct)
    {
        Strategy<GrpcInteraction> calls = Strategy.Grpc.Unary(
            resourceName: "greeter",
            method: Greeter.Descriptor.FindMethodByName("SayHello").ToGrpcMethod<HelloRequest, HelloReply>(),
            requestStrategy: Strategy.FromProtobuf<HelloRequest>());

        await Property.ForAll(target, calls, async (t, call) =>
        {
            GrpcResponse response = (GrpcResponse)(await t.ExecuteAsync(call, ct))!;
            response.AssertStatusOk();
        }, ct);
    }
}
```

`Strategy.FromProtobuf<HelloRequest>()` (from `Conjecture.Protobuf`) generates the protobuf request from its descriptor. `Greeter.Descriptor` is the static descriptor on the generated `Greeter` base class.

## Test a server-streaming call

Server-stream calls send one request and receive a stream of responses. The full response stream is materialised before `ExecuteAsync` returns (up to `MaxResponseMessages`, default 1024).

```csharp
[Property]
public async Task ListItems_NeverReturnsUnknown(CancellationToken ct)
{
    Strategy<GrpcInteraction> calls = Strategy.Grpc.ServerStream(
        resourceName: "greeter",
        method: ItemServiceDescriptors.ListItemsMethod,
        requestStrategy: Strategy.FromProtobuf<ListItemsRequest>());

    await Property.ForAll(target, calls, async (t, call) =>
    {
        GrpcResponse response = (GrpcResponse)(await t.ExecuteAsync(call, ct))!;
        response.AssertNoUnknownStatus();
    }, ct);
}
```

`AssertNoUnknownStatus` catches the case where the server throws an unhandled exception, which gRPC surfaces as `StatusCode.Unknown`. It accepts any other status code (including `OK`, `NotFound`, `DeadlineExceeded`) so it tolerates expected failures while flagging catastrophic ones.

## Test a client-streaming call with sequence shrinking

Client-stream calls send N requests and receive one response. Pass a `Strategy<IReadOnlyList<TReq>>` — the list strategy from `Conjecture.Core` shrinks both the list length and each element, so a failing test reduces to the minimum-length sequence that still reproduces the bug.

```csharp
[Property]
public async Task UploadBatch_AcceptsAnySequence(CancellationToken ct)
{
    Strategy<IReadOnlyList<UploadChunk>> chunks =
        Strategy.List(Strategy.FromProtobuf<UploadChunk>(), minLength: 0, maxLength: 100);

    Strategy<GrpcInteraction> calls = Strategy.Grpc.ClientStream(
        resourceName: "greeter",
        method: UploadServiceDescriptors.UploadBatchMethod,
        requestsStrategy: chunks);

    await Property.ForAll(target, calls, async (t, call) =>
    {
        GrpcResponse response = (GrpcResponse)(await t.ExecuteAsync(call, ct))!;
        response.AssertStatusOk();
    }, ct);
}
```

When this fails on a sequence of 17 chunks, the shrinker first reduces the list to one chunk, then shrinks that chunk's protobuf fields — same deterministic replay as any other Conjecture test.

## Test a bidi-streaming call

Bidi calls interleave N requests and N responses. The strategy shape is the same as client-stream — a `Strategy<IReadOnlyList<TReq>>` on the request side. Responses materialise after the call closes.

```csharp
[Property]
public async Task EchoStream_PreservesOrder(CancellationToken ct)
{
    Strategy<IReadOnlyList<EchoRequest>> messages =
        Strategy.List(Strategy.FromProtobuf<EchoRequest>(), minLength: 1, maxLength: 16);

    Strategy<GrpcInteraction> calls = Strategy.Grpc.BidiStream(
        resourceName: "greeter",
        method: EchoServiceDescriptors.EchoMethod,
        requestsStrategy: messages);

    await Property.ForAll(target, calls, async (t, call) =>
    {
        GrpcResponse response = (GrpcResponse)(await t.ExecuteAsync(call, ct))!;
        response.AssertStatusOk();
    }, ct);
}
```

## Test against a remote endpoint

`GrpcChannelTarget` wraps a real `GrpcChannel` for tests against containerised or staging services. The dispatch path is identical — only the target construction changes.

```csharp
await using GrpcChannelTarget target = new("greeter", "http://localhost:5001");
// ... same Property.ForAll calls ...
```

Disposing the target shuts the channel down. Caller owns the address string and any TLS configuration via `GrpcChannelOptions`.

## Per-runner adapters

The xUnit v3 example above uses `[Property]` from `Conjecture.Xunit.V3`. Switch the test attribute and the package; everything inside the test body is identical.

| Runner | Package | Test attribute / shape |
|---|---|---|
| xUnit v3 | `Conjecture.Xunit.V3` | `[Property]` on an `async Task(CancellationToken)` method |
| xUnit v2 | `Conjecture.Xunit` | `[Property]` on an `async Task(CancellationToken)` method |
| NUnit | `Conjecture.NUnit` | `[Property]` on an `async Task(CancellationToken)` method |
| MSTest | `Conjecture.MSTest` | `[Property]` on an `async Task(CancellationToken)` method |
| TestingPlatform | `Conjecture.TestingPlatform` | `[Property]` on an `async Task(CancellationToken)` method |
| Expecto | `Conjecture.FSharp.Expecto` | `testProperty "..." <| fun (...) -> ...` |
| .NET Interactive | `Conjecture.Interactive` + `Conjecture.Core` | imperative `await Property.ForAll(...)` in a code cell |
| LinqPad | `Conjecture.LinqPad` + `Conjecture.Core` | imperative `await Property.ForAll(target, strategy, assertion, QueryCancelToken)` |

The `Property.ForAll(target, strategy, assertion, ct)` shape is the project's universal entry point — Interactive notebooks and LinqPad scripts call it directly without an attribute layer.

## Status invariants reference

```csharp
response.AssertStatusOk();                    // StatusCode.OK
response.AssertStatus(StatusCode.NotFound);   // a specific code
response.AssertNoUnknownStatus();             // any code except StatusCode.Unknown
```

All three return `response` for chaining and throw `GrpcInvariantException` (with status, detail, trailers in the message) on mismatch.

## See also

- [gRPC property testing — concepts](../explanation/grpc-property-testing.md)
- [ADR 0062 — Conjecture.Grpc package design](../decisions/0062-conjecture-grpc-package-design.md)
- [Generate Protobuf message payloads](generate-protobuf-payloads.md)
