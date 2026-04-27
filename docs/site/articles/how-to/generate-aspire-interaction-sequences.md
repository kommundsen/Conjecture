# Generate interleaved Aspire interaction sequences

`AspireInteractionSequenceBuilder` composes HTTP step strategies, messaging step strategies, and `DbSnapshot` steps into a single `Strategy` that generates interleaved sequences. Conjecture treats the sequence as a unit during shrinking — it finds the minimal sequence of interactions that exposes a DB consistency violation.

## When to use it

- You want property tests that cover interaction *sequences*, not just single-shot requests.
- You need to assert on DB state between steps (e.g., the row must appear before the second HTTP call proceeds).
- You are testing cross-service flows that interleave HTTP calls, message publications, and DB assertions.

## Prerequisites

A configured fixture — see [Tutorial 12](../tutorials/12-aspire-efcore-integration.md).

## Basic sequence

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Http;

AspireInteractionSequenceBuilder builder = new();

Strategy<IReadOnlyList<IAddressedInteraction>> sequences =
    builder
        .Http("orders-api", postOrderStrategy)
        .DbSnapshot(
            "orders-db",
            "after-place",
            ctx => ctx.Orders.CountAsync().ContinueWith(t => (object?)t.Result))
        .Message("orders", shipCommandStrategy)
        .DbSnapshot(
            "orders-db",
            "after-ship",
            ctx => ctx.Orders
                .Select(o => (object?)o.Status)
                .FirstOrDefaultAsync())
        .Build();
```

The builder produces a `Strategy<IReadOnlyList<IAddressedInteraction>>` that generates sequences by picking steps uniformly from all registered step strategies. Each `DbSnapshot` step is deterministic — it appears in every generated sequence.

## `Http` step

```csharp
builder.Http(
    resourceName: "orders-api",
    step: Generate.From<Order>().Select(o => new HttpInteraction(
        ResourceName: "orders-api",
        Method: "POST",
        Path: "/orders",
        Body: o,
        Headers: null)));
```

`resourceName` overwrites `HttpInteraction.ResourceName` so the composite target routes the step to the correct `HttpClient` regardless of how the strategy was originally constructed.

## `Message` step

```csharp
builder.Message(
    resourceName: "orders",
    step: Generate.From<ShipCommand>().Select(cmd => new MessageInteraction(
        Destination: "orders",
        Body: JsonSerializer.SerializeToUtf8Bytes(cmd),
        Headers: ReadOnlyDictionary<string, string>.Empty,
        MessageId: Guid.NewGuid().ToString())));
```

`resourceName` is used as the routing address for the resulting `AddressedMessageInteraction`.

## `DbSnapshot` step

```csharp
builder.DbSnapshot(
    resourceName: "orders-db",
    label: "after-place",
    capture: ctx => ctx.Orders.CountAsync().ContinueWith(t => (object?)t.Result))
```

`DbSnapshot` stores a labelled `Func<DbContext, Task<object?>>`. The captured value is serialized into the interaction trace — both for human reading and for Conjecture's shrinking replay. The `label` appears verbatim in the failure report.

## Full builder reference

```csharp
AspireInteractionSequenceBuilder builder = new();

builder
    .Http(string resourceName, Strategy<HttpInteraction> step)
    .Message(string resourceName, Strategy<MessageInteraction> step)
    .DbSnapshot(string resourceName, string label, Func<DbContext, Task<object?>> capture)
    .Build(int minSize = 1, int maxSize = 20)
// returns Strategy<IReadOnlyList<IAddressedInteraction>>
```

All builder methods are chainable and return `AspireInteractionSequenceBuilder`. `Build()` returns a `Strategy<IReadOnlyList<IAddressedInteraction>>` that generates sequences of length in [`minSize`, `maxSize`].

## Executing the sequence

Use the generated sequence inside a `[Property]` test:

```csharp
using Conjecture.Aspire.EFCore;
using Conjecture.Core;
using Conjecture.Http;
using Conjecture.Interactions;
using Conjecture.Xunit.V3;

public class OrderSequenceTests(StoreFixture fixture)
    : IClassFixture<StoreFixture>
{
    private readonly Strategy<IReadOnlyList<IAddressedInteraction>> sequences =
        new AspireInteractionSequenceBuilder()
            .Http("orders-api", fixture.PostOrderStrategy)
            .Message("orders", fixture.ShipCommandStrategy)
            .Build();

    [Property]
    public async Task PlaceAndShip_LeaveConsistentState(
        Strategy<IReadOnlyList<IAddressedInteraction>> seqs)
    {
        IReadOnlyList<IAddressedInteraction> sequence = seqs.Sample();

        foreach (IAddressedInteraction step in sequence)
        {
            await fixture.CompositeTarget.ExecuteAsync(step, default);
        }
    }
}
```

## Reading the trace

When a step fails, Conjecture shrinks the sequence and prints the minimal failing trace:

```text
Conjecture.Aspire.EFCore.AspireEFCoreInvariantException:
Sequence failed at step 3 (Message orders).

Step 1: Http POST /orders → 201 Created
  DbSnapshot 'after-place': orders-db Order count = 1

Step 2: Message orders → ACK
  DbSnapshot 'after-ship': orders-db Order.Status = null

Expected 'after-ship' to contain "Shipped"; got null.
```

The shrunk sequence is the shortest that reproduces the failure. Here it reveals that the ship consumer ACKs the message without updating `Order.Status`.

## Shrinking sequences

Conjecture shrinks sequences by:

1. Removing steps from the tail — shortest sequence first.
2. Simplifying payloads within each step — smallest value that still triggers the failure.
3. Combining: a two-step sequence with a large payload shrinks to a two-step sequence with the minimal payload, not a one-step sequence.

`DbSnapshot` steps are not removed during shrinking — they are observations, not interactions. Shrinking focuses on the HTTP and message steps.

## See also

- [Tutorial 12: Composite property tests for Aspire + EF Core](../tutorials/12-aspire-efcore-integration.md)
- [Reference: AspireInteractionSequenceBuilder](../reference/aspire-efcore.md#aspireinteractionsequencebuilder)
- [Reference: DbSnapshotInteraction](../reference/aspire-efcore.md#dbsnapshotinteraction)
- [How-to: Test eventual consistency](test-aspire-efcore-eventual-consistency.md)
