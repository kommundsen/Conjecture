# gRPC property testing

Conjecture's `Conjecture.Grpc` package lets you write property tests against gRPC services the same way you write them against HTTP endpoints or message queues. This page explains the model: how `GrpcInteraction` covers all four call modes, where in-stream sequence shrinking comes from, and what trade-offs the design accepts.

## The two-layer model

gRPC support sits on top of the v0.20 [transport-agnostic interaction foundation](../decisions/0059-conjecture-interactions-and-http-architecture.md) (`Conjecture.Interactions`):

- **Layer 1** — `IInteraction`, `IInteractionTarget`, `InteractionStateMachine<TState>`. Knows nothing about gRPC. The shrinker and state-machine machinery live here.
- **Layer 2** — `Conjecture.Grpc`. Adds `GrpcInteraction`, `GrpcResponse`, `IGrpcTarget`, `GrpcChannelTarget`, and `HostGrpcTarget`. Layer 2 is one package: `Microsoft.AspNetCore.Mvc.Testing` is small enough that splitting `HostGrpcTarget` into a satellite buys nothing.

The same `Property.ForAll(target, strategy, assertion, ct)` shape works against any gRPC target. Switching from a remote endpoint to an in-process `WebApplicationFactory` host is a one-line change.

## What `GrpcInteraction` carries

```csharp
public sealed record GrpcInteraction(
    string ResourceName,
    string FullMethodName,
    GrpcRpcMode Mode,
    IReadOnlyList<ReadOnlyMemory<byte>> RequestMessages,
    IReadOnlyDictionary<string, string> Metadata,
    TimeSpan? Deadline = null) : IInteraction;
```

- `ResourceName` is the logical channel name routed by `CompositeInteractionTarget`. Use it to fan one test across multiple gRPC channels.
- `FullMethodName` is the gRPC `/package.Service/Method` string used by `CallInvoker`. Kept separate from `ResourceName` so a single channel can carry multiple methods.
- `Mode` is one of `Unary`, `ServerStream`, `ClientStream`, `Bidi`. The mode dictates how `RequestMessages` and the response are interpreted.
- `RequestMessages` is `IReadOnlyList<ReadOnlyMemory<byte>>`. Bytes — not typed messages — keep the shape uniform across protobuf, JSON-transcoded, and arbitrary-payload tests, mirroring `MessageInteraction.Body`. Typed serialisation lives in `Generate.Grpc.*`, not on the interaction itself. Unary and server-stream calls always carry exactly one element; client-stream and bidi accept any non-negative count (the shrinker reduces it).
- `Metadata` is the gRPC metadata dictionary. Lower-case ASCII keys per gRPC convention; the package does not normalise — incorrect keys surface as runtime errors in the target, matching how HTTP headers are handled.
- `Deadline` is nullable. When set, the target wires it through `CallOptions.WithDeadline(...)`.

## Why one record for all four modes

`GrpcInteraction` does not have a discriminated-union shape (`UnaryGrpcInteraction`, `ServerStreamGrpcInteraction`, etc.). One record with a `Mode` enum keeps `IInteractionTarget.ExecuteAsync` uniform — `CompositeInteractionTarget` routes by `ResourceName` without inspecting the call mode, the shrinker has one shape to reason about, and downstream tooling (telemetry, scaffolds, MCP tools) handles all four modes the same way.

The cost is that unary and server-stream callers wrap a single message in a one-element list. That is paid once in the strategy builder and disappears at the call site:

```csharp
Strategy<GrpcInteraction> unary = Generate.Grpc.Unary(
    resourceName: "greeter",
    method: GreeterService.SayHelloMethod,
    requestStrategy: Generate.FromProtobuf<HelloRequest>());
```

## In-stream sequence shrinking

For client-stream and bidi calls, the request stream is a *sequence of messages within a single RPC*. Two things are interesting about that:

1. The sequence needs to shrink — failing tests should reduce to the minimum number of messages that still trigger the bug.
2. The sequence lives inside a single `GrpcInteraction`, not across multiple steps in an `InteractionStateMachine`. The existing `CommandSequenceShrinkPass` shrinks across steps, not within them.

The package's design answer is to shrink at the strategy layer, not the state-machine layer:

```
Strategy<IReadOnlyList<TReq>>            (length + per-element shrinking from Conjecture.Core)
   .Select(reqs => reqs.Select(Serialize).ToArray())
   .Select(bytes => new GrpcInteraction(..., RequestMessages: bytes, ...))
```

`Conjecture.Core`'s list-strategy already deletes elements one at a time and shrinks per-element values. This composes directly: the shrinker first reduces the list to a single failing message, then shrinks that message's protobuf payload. **No new shrinking machinery is required**, and Layer 1 stays unchanged. That is the architectural answer to the open question ADR 0059 deferred ("Layer 1 may need extension points when a second transport ships"): in-stream sequences are a Layer 2 concern handled by strategy composition, not a Layer 1 contract change.

## How dispatch works

Both `GrpcChannelTarget` and `HostGrpcTarget` dispatch via `Grpc.Net.Client.CallInvoker` — the abstract base type that `GrpcChannel.CreateCallInvoker()` returns. The legacy `Grpc.Core` native binding is deprecated by Google as of 2024 and not used. Method descriptors come from generated `Grpc.Tools` clients (`Method<TReq, TResp>` instances exposed as static fields on the generated base type) — the property test passes the descriptor into `Generate.Grpc.*`, and the strategy serialises requests through the descriptor's `RequestMarshaller`.

Server-stream and bidi responses are materialised in full before `ExecuteAsync` returns, so assertions run against the complete response after the call. This is a deliberate trade-off: streaming-as-iteration would require re-running the call to replay a shrunk interaction, which breaks deterministic shrinking when the server has side effects. A `MaxResponseMessages` setting on each target (default 1024) prevents runaway streams from exhausting memory.

## Status assertions

`GrpcInvariantExtensions` adds three extension methods to `GrpcResponse`:

```csharp
response.AssertStatusOk();                    // StatusCode.OK
response.AssertStatus(StatusCode.NotFound);   // a specific code
response.AssertNoUnknownStatus();             // any code except StatusCode.Unknown
```

Each returns `response` so calls chain. Failure messages include the actual status, status detail, and trailers — the same debuggability shape as `HttpInvariantExtensions`. Use `.AssertNoUnknownStatus()` for fuzzing scenarios where you expect *some* failure but want to catch the catastrophic-bug case where the server returns `Unknown` (typically an unhandled server-side exception).

## What you give up

Materialising server-stream responses before `ExecuteAsync` returns means partial-stream assertions are not supported. Tests that need them can write a custom target around `CallInvoker` directly — but the shrinker's replay guarantee then becomes your responsibility.

`RequestMessages.IReadOnlyList<ReadOnlyMemory<byte>>` is uniform across modes, but unary callers pay the cost of a one-element wrapper. The strategy builders absorb this so it is invisible at the call site.

Binary metadata (`-bin` keys per gRPC spec) is not modelled — `Metadata` is `IReadOnlyDictionary<string, string>`. Tests that depend on binary metadata can encode through Base64 in the meantime; first-class binary support lands when a real test surfaces the need.

For the full design rationale see [ADR 0062](../decisions/0062-conjecture-grpc-package-design.md).
