# 0062. Conjecture.Grpc package design

**Date:** 2026-04-26
**Status:** Accepted

## Context

ADR 0059 ships `Conjecture.Interactions` (`IInteraction`, `IInteractionTarget`, `InteractionStateMachine`, `CompositeInteractionTarget`) plus the HTTP concretion (`Conjecture.Http`) as the v0.20 interaction foundation. ADR 0061 adds `Conjecture.Messaging` as the first non-HTTP transport, validating Layer 1 against pull-based queue/topic semantics.

gRPC is the second non-HTTP transport. It is the stress test for `IInteraction`: unlike HTTP one-shot or messaging publish/consume, three of the four gRPC modes (server-stream, client-stream, bidirectional-stream) involve **sequences of messages within a single RPC call**. Whether Layer 1 can express that shape — and whether the existing `CommandSequenceShrinkPass` is enough to shrink it — gates whether the abstraction generalises beyond request/response.

The decision must answer:

- What does a `GrpcInteraction` carry, given the four call modes?
- How are in-stream message sequences shrunk inside a single interaction?
- How does dispatch work — `Grpc.Core` `CallInvoker`, `GrpcChannel`, or both?
- How do concrete adapters ship — bundled or as satellite sub-packages?
- How do request-body strategies compose with the v0.21 Protobuf strategies (#440, #441)?
- What is the per-adapter test strategy?

## Decision

Ship a single `Conjecture.Grpc` NuGet package containing the transport primitives, both targets, the `Generate.Grpc` strategy builder, and the invariant helpers. No satellite sub-packages — `Grpc.Net.Client` and `Microsoft.AspNetCore.Mvc.Testing` are the only meaningful production dependencies and both are already standard in the gRPC ecosystem.

### Layer 1 (gRPC concretion over `Conjecture.Interactions`)

**`GrpcInteraction : IInteraction`** — immutable record covering all four modes:

```csharp
public sealed record GrpcInteraction(
    string ResourceName,
    string FullMethodName,
    GrpcRpcMode Mode,
    IReadOnlyList<ReadOnlyMemory<byte>> RequestMessages,
    IReadOnlyDictionary<string, string> Metadata,
    TimeSpan? Deadline = null) : IInteraction;

public enum GrpcRpcMode { Unary, ServerStream, ClientStream, Bidi }
```

- `ResourceName` is the logical channel name routed by `CompositeInteractionTarget` (matches the HTTP and messaging conventions; lets multi-host scenarios share one shape).
- `FullMethodName` is the gRPC `/package.Service/Method` string used by `CallInvoker`. Kept separate from `ResourceName` so a single channel can carry multiple methods.
- `Mode` selects the call shape. Unary and server-stream require `RequestMessages.Count == 1`; client-stream and bidi accept any non-empty count (the shrinker reduces it).
- `RequestMessages` is `IReadOnlyList<ReadOnlyMemory<byte>>`. Bytes — not typed messages — keep the shape uniform across protobuf, JSON-transcoded, and arbitrary-payload tests, mirroring `MessageInteraction.Body` (ADR 0061). Typed serialisation lives in `Generate.Grpc.*`, not on the interaction itself.
- `Metadata` is the gRPC metadata (custom headers, auth tokens). Lower-case ASCII keys per gRPC spec; the package does not normalise — incorrect keys surface as runtime errors in the target, matching how HTTP headers are handled.
- `Deadline` is nullable; when set the target wires it through `CallOptions.WithDeadline(...)`.

**`GrpcResponse`** — uniform result shape returned from `ExecuteAsync` (boxed as `object?` to satisfy the Layer 1 contract):

```csharp
public sealed record GrpcResponse(
    StatusCode Status,
    string? StatusDetail,
    IReadOnlyList<ReadOnlyMemory<byte>> ResponseMessages,
    IReadOnlyDictionary<string, string> ResponseHeaders,
    IReadOnlyDictionary<string, string> Trailers);
```

`StatusCode` is `Grpc.Core.StatusCode` (the same enum is shared by `Grpc.Net.Client` and `Grpc.Core` — re-using it avoids a parallel hierarchy). Server-stream and bidi responses populate `ResponseMessages` with the full server-side stream materialised before the call returns. Streaming-as-iteration is rejected below.

**`IGrpcTarget : IInteractionTarget`** — Layer 1 contract carries no extra surface beyond what `IInteractionTarget` provides:

```csharp
public interface IGrpcTarget : IInteractionTarget
{
    CallInvoker GetCallInvoker(string resourceName);
}
```

`GetCallInvoker` exists for advanced users who want to reach past `GrpcInteraction` and run a hand-written `CallInvoker.AsyncUnaryCall` against the same channel as the property test (mirrors `IHttpTarget.ResolveClient`). Default `ExecuteAsync` implementation lives on an internal helper so adapters only have to override `GetCallInvoker`.

### Streaming shrinking — sequence shrinking *within* a single interaction

In-stream message sequences are shrunk by **the strategy**, not by the state-machine shrinker. `Generate.Grpc.ClientStream(method, reqStrategy)` returns `Strategy<GrpcInteraction>` whose underlying choice tree is:

```
Strategy<IReadOnlyList<TReq>>            (length + per-element shrinking from Conjecture.Core)
   .Select(reqs => reqs.Select(Serialize).ToArray())
   .Select(bytes => new GrpcInteraction(..., RequestMessages: bytes, ...))
```

The list-shrinking primitive in `Conjecture.Core` (used by `Generate.List<T>`) already deletes elements one at a time and shrinks per-element values. This composes directly: the shrinker first reduces the list to a single failing message, then shrinks that message's protobuf payload. **No new shrinking machinery is required.**

This is the architectural answer to the question ADR 0059 deferred ("Layer 1 may need extension points when a second transport ships"): in-stream sequences are a Layer 2 concern handled by strategy composition, not a Layer 1 contract change. `IInteraction` and `IInteractionTarget` ship unchanged.

### Dispatch via `Grpc.Net.Client.CallInvoker`

Both targets dispatch via `CallInvoker` (the abstract base type that `GrpcChannel.CreateCallInvoker()` returns). `Grpc.Core` is deprecated by Google as of 2024 and not used. Method descriptors come from generated `Grpc.Tools` clients (`Method<TReq, TResp>` instances exposed as static fields on the generated base type) — the property test passes the descriptor into `Generate.Grpc.*`.

### Layer 2 — Concrete targets (same package)

- **`GrpcChannelTarget : IGrpcTarget`** — wraps a `GrpcChannel` created from `GrpcChannel.ForAddress(url, options)`. For real services and integration tests against a loopback gRPC server.
- **`HostGrpcTarget : IGrpcTarget`** — wraps an `IHost` whose Kestrel server hosts gRPC services, plus a `GrpcChannel` built from `WebApplicationFactory<TStartup>.Server.CreateHandler()`. Async-disposable; reuses the `IHost` lifecycle pattern from `HostHttpTarget` (ADR 0059).

Both ship in `Conjecture.Grpc`. No satellite packages — unlike messaging where Azure Service Bus and RabbitMQ each pull in megabytes of broker SDK, the gRPC story is uniform: one `Grpc.Net.Client` reference covers every consumer. Splitting `HostGrpcTarget` into `Conjecture.Grpc.AspNetCore` was considered and rejected (see Alternatives).

### `Generate.Grpc` strategy builder

```csharp
Generate.Grpc.Unary<TReq, TResp>(Method<TReq, TResp> method, Strategy<TReq> reqStrategy);
Generate.Grpc.ServerStream<TReq, TResp>(Method<TReq, TResp> method, Strategy<TReq> reqStrategy);
Generate.Grpc.ClientStream<TReq, TResp>(Method<TReq, TResp> method, Strategy<IReadOnlyList<TReq>> reqsStrategy);
Generate.Grpc.BidiStream<TReq, TResp>(Method<TReq, TResp> method, Strategy<IReadOnlyList<TReq>> reqsStrategy);
```

All four return `Strategy<GrpcInteraction>`. They serialise via `Method<TReq, TResp>.RequestMarshaller.ContextualSerializer` so any user-supplied `Method<,>` descriptor (proto-generated, custom marshallers, or hand-rolled) works. The protobuf strategies from v0.21 (`Generate.FromProtobuf<T>()` — #440, #441) plug straight into `reqStrategy` / `reqsStrategy`.

Convenience overloads taking `Generate.List(reqStrategy, minLength: 1)` for client-stream / bidi callers who don't want to compose the list themselves.

### Invariant helpers

```csharp
GrpcResponse.AssertStatusOk();                 // StatusCode.OK
GrpcResponse.AssertStatus(StatusCode expected);
GrpcResponse.AssertNoUnknownStatus();          // any code except StatusCode.Unknown
```

Match the `HttpInvariantExtensions` shape (ADR 0059): extension methods on the response type, throwing `InvariantViolationException` with a message that includes the actual status, status detail, and trailers for debuggability.

### Per-adapter test strategy

Two tiers, mirroring ADR 0061:

- **Unit tier** (`Conjecture.Grpc.Tests`) — fake `CallInvoker` that records calls and returns canned responses. Covers the `GrpcInteraction` ↔ `CallInvoker` translation, the four call-mode dispatch paths, and the strategy/shrinking composition. No network, runs in milliseconds, on every PR.
- **Integration tier** (`Conjecture.SelfTests/Grpc/`) — real `Grpc.AspNetCore` server hosted in-process via `WebApplicationFactory<Startup>`. Class-scoped fixture (one host + channel per test class). Covers `GrpcChannelTarget` and `HostGrpcTarget` against a real protobuf service. Runs on CI nightly and on PRs labelled `integration`.

Same skip-when-unavailable pattern as messaging: integration tests gated on a sentinel env var (or simply on the test project's presence) so the local dev loop on a no-Docker / no-network machine stays fast.

## Consequences

**Easier:**
- Same `Property.ForAll(target, strategy, assertion, ct)` shape as HTTP and messaging — no new primitives.
- All four call modes share one `GrpcInteraction` shape, so `CompositeInteractionTarget` routing and downstream tooling (telemetry, scaffolds) handle them uniformly.
- In-stream sequence shrinking falls out of existing list-strategy shrinking; no new shrink pass needed.
- Protobuf strategies from v0.21 plug in directly via `Method<,>.RequestMarshaller`.

**Harder:**
- `RequestMessages` as `IReadOnlyList<ReadOnlyMemory<byte>>` forces unary/server-stream to wrap a single message in a one-element list. Acceptable: makes the four modes uniform and saves a discriminated-union shape on the interaction.
- Server-stream / bidi responses are materialised in full before `ExecuteAsync` returns — long-running streams cannot be partially asserted on. Acceptable for property testing (assertions run after the call) and matches the request side's eager materialisation.
- Trailers and headers come back as `IReadOnlyDictionary<string, string>`; binary metadata (`-bin` keys) is not modelled. Document as out-of-scope for v0.21; revisit if a real test surfaces a need.

**Risks:**
- Layer 1 stays unchanged on the basis that streaming shrinking lives in strategy composition. If a future transport (SignalR, WebRTC) needs *cross-step* in-stream coordination that the state machine cannot express, `IInteractionTarget` may still need extension. Defer until that transport actually exists; matches ADR 0059's "minimal until needed" policy.
- `Grpc.Net.Client` 3.0 changed `WebApplicationFactory` integration semantics in 2025; `HostGrpcTarget` is pinned to a specific compatibility shape. Tracked in the integration tier — if the SDK changes again, the adapter's tests catch it before users do.
- Materialising server-stream responses fully is fine for finite streams but is a hazard for unbounded streams. Document a `MaxResponseMessages` setting on `GrpcChannelTarget` / `HostGrpcTarget` (default 1024) so a runaway stream throws rather than exhausts memory.

## Alternatives Considered

**`Conjecture.Grpc` + satellite `Conjecture.Grpc.AspNetCore`** — split the `HostGrpcTarget` (which depends on `Microsoft.AspNetCore.Mvc.Testing`) into its own NuGet. Rejected: `WebApplicationFactory` is the only realistic in-process host mechanism for gRPC, every gRPC test consumer wants it, and `Microsoft.AspNetCore.Mvc.Testing` is a small dependency. The messaging precedent for satellite packages applied because each broker SDK was 2–5 MB; `Microsoft.AspNetCore.Mvc.Testing` does not justify the same split.

**Streaming as iteration — `IAsyncEnumerable<ReadOnlyMemory<byte>>` on `GrpcInteraction`** — model server-side response streams as `IAsyncEnumerable<TResp>` so users can assert on partial streams. Rejected: the shrinker needs deterministic replay. An `IAsyncEnumerable` consumed once cannot be replayed against a shrunk interaction without re-running the call, and re-running breaks deterministic shrinking when the server has side effects. Materialising the full response stream up to `MaxResponseMessages` keeps replay deterministic; users who need partial-stream assertions can write them inline before the call returns by composing a custom target.

**One `GrpcInteraction` subtype per mode** — `UnaryGrpcInteraction`, `ServerStreamGrpcInteraction`, etc. Rejected: forces `IInteractionTarget.ExecuteAsync` to do a `switch` on the runtime type, fragments the shrinker's view of the interaction (each subtype would need its own list-shrinking glue), and breaks `CompositeInteractionTarget`'s uniform routing. The single record + `Mode` enum is what HTTP and messaging both do.

**Use `Grpc.Core.CallInvoker` on .NET (the legacy native binding)** — for parity with the older Hypothesis-Python gRPC story. Rejected: `Grpc.Core` is deprecated by Google in 2024 and security-only since. `Grpc.Net.Client.CallInvoker` is the supported path on modern .NET and uses `HttpClient` underneath, which composes with `WebApplicationFactory` for free.

**In-stream sequence shrinking via a new `IStreamShrinkPass`** — a dedicated shrink pass for the request-message list. Rejected: list-strategy shrinking from `Conjecture.Core` already does length reduction and per-element shrinking. A dedicated pass would duplicate the same logic without adding signal. If a future need arises (e.g. cross-stream invariants), revisit.
