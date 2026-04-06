# Draft: Aspire Integration for Distributed Property Testing

## Motivation

Modern .NET applications are distributed systems — microservices communicating over HTTP, gRPC, and message queues, orchestrated by .NET Aspire. Testing these systems for correctness is hard: individual unit tests pass, but emergent behavior across services breaks under specific interaction patterns. Property-based testing can systematically explore these interaction spaces, and Aspire's orchestration model provides the infrastructure to spin up, configure, and tear down multi-service environments for each test run.

## .NET Advantage

.NET Aspire provides a programmatic app model (`IDistributedApplicationBuilder`) that defines services, dependencies, and connections as code. Aspire's `DistributedApplicationTestingBuilder` already supports integration testing by spinning up the full app model in-process. Conjecture can generate request sequences, message payloads, and timing variations, then shrink failing interaction traces to minimal reproductions — all within Aspire's managed lifecycle.

## Key Ideas

### Property Tests Against Aspire App Models
```csharp
[Property]
public async Task OrderFlowMaintainsConsistency(
    Strategy<CreateOrderRequest> orderRequest,
    Strategy<PaymentEvent> paymentEvent)
{
    await using var app = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.MyStore_AppHost>();

    await app.StartAsync();

    var orderClient = app.CreateHttpClient("order-service");
    var paymentClient = app.CreateHttpClient("payment-service");

    var order = await orderClient.PostAsJsonAsync("/orders", orderRequest);
    await paymentClient.PostAsJsonAsync("/payments", paymentEvent);

    // Invariant: order state is consistent across services
    var orderState = await orderClient.GetFromJsonAsync<Order>($"/orders/{order.Id}");
    var paymentState = await paymentClient.GetFromJsonAsync<Payment>($"/payments/{order.Id}");

    Assert.Equal(orderState.Status == "paid", paymentState.Status == "completed");
}
```

### Interaction Sequence Generation
```csharp
// Generate sequences of API calls across services
var interactionStrategy = Generate.Lists(
    Generate.OneOf(
        createOrderStrategy.Select(r => new Interaction("order-service", "POST", "/orders", r)),
        updateInventoryStrategy.Select(r => new Interaction("inventory-service", "PUT", "/stock", r)),
        processPaymentStrategy.Select(r => new Interaction("payment-service", "POST", "/pay", r))
    ),
    minSize: 1, maxSize: 20
);
```

### Interaction Trace Shrinking
- When a property fails, the interaction sequence is shrunk to the minimal set of API calls that reproduce the failure
- Leverages existing command sequence shrinking from stateful testing (Phase 4)
- Shrink dimensions: fewer interactions, simpler payloads, reduced concurrency

### Timing and Concurrency Variation
- Generate delays between interactions to expose race conditions
- Generate concurrent request batches to test under load
- Shrink toward sequential, zero-delay execution for minimal reproduction

### Aspire Resource Strategies
```csharp
// Generate configuration variations
var configStrategy = Generate.Compose<AspireConfig>(ctx => new AspireConfig
{
    DatabaseProvider = ctx.Generate(Generate.OneOf("postgres", "sqlserver")),
    CacheEnabled = ctx.Generate(Generate.Booleans()),
    RetryCount = ctx.Generate(Generate.Integers<int>(0, 5))
});
```

## Design Decisions to Make

1. Ship as `Conjecture.Aspire` package or integrate into existing test adapters?
2. How to manage Aspire app lifecycle per property example? (Startup cost is significant — share across examples or isolate?)
3. Should interaction strategies be built on top of stateful testing (`IStateMachine`) or a new abstraction?
4. How to handle flaky distributed tests? (Network timeouts, container startup race conditions)
5. What Aspire resources should have built-in strategies? (HTTP endpoints, message queues, databases, caches)
6. How to report failures — include full interaction trace, service logs, timing diagram?

## Scope Estimate

Large. Requires Aspire testing infrastructure integration, interaction sequence modeling, and distributed shrinking. ~4-5 cycles.

## Dependencies

- `Aspire.Hosting.Testing` NuGet package
- `Conjecture.Core` strategy engine and stateful testing
- Existing command sequence shrinking (`CommandSequenceShrinkPass`)
- .NET 10 + Aspire 9.x (or later)

## Open Questions

- How expensive is spinning up an Aspire app model per property run? Can we amortize across examples?
- Should Conjecture provide Aspire-aware `IStateMachine` implementations out of the box?
- How to handle service discovery and dynamic port allocation in generated interactions?
- Is there value in generating infrastructure variations (e.g., test with Redis vs in-memory cache)?
- How to integrate with Aspire's built-in health checks and readiness probes?
- Should failure reports include links to Aspire dashboard traces?
