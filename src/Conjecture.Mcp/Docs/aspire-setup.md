# Conjecture.Aspire Setup Guide

Step-by-step guide for wiring up `Conjecture.Aspire` in a test project.

## 1. Add the NuGet package

```xml
<PackageReference Include="Conjecture.Aspire" />
```

## 2. Implement `IAspireAppFixture`

`IAspireAppFixture` is the contract your test fixture must implement. It provides the running `DistributedApplication` and exposes `ResetAsync()` to restore application state between property iterations.

```csharp
using Conjecture.Aspire;

public class MyAppFixture : IAspireAppFixture, IAsyncLifetime
{
    public DistributedApplication App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.MyAppHost>();
        App = await appHost.BuildAsync();
        await App.StartAsync();
    }

    public Task ResetAsync()
    {
        // Restore any state changed during a property iteration.
        // Called automatically by AspireStateMachine<TState> between runs.
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
    }
}
```

### `ResetAsync` lifecycle

`ResetAsync` is called by the framework after each property iteration to guarantee a clean slate for the next run. Typical implementations truncate database tables, flush queues, or reset in-memory caches.

## 3. Use `AspireStateMachine<TState>`

`AspireStateMachine<TState>` drives stateful end-to-end property tests against the running Aspire application. Pair it with an `IStateMachine<TState, TCommand>` implementation:

```csharp
using Conjecture.Aspire;

public class MyStateMachine : IStateMachine<MyState, Interaction>
{
    // Implement state transitions
}
```

Wire the state machine in your property test:

```csharp
[Property]
public async Task AppBehavesCorrectly(IAspireAppFixture fixture)
{
    AspireStateMachine<MyState> machine = new(new MyStateMachine(), fixture.App);
    await machine.RunAsync();
}
```

## 4. Generate interactions

Use `Generate.HttpPost` and `Generate.PublishMessage` to produce interaction strategies:

```csharp
Generate.HttpPost("/api/orders", Generate.Strings())
// → Strategy<Interaction> for HTTP POST interactions

Generate.PublishMessage("orders-queue", Generate.Strings())
// → Strategy<Interaction> for message-publishing interactions
```

## 5. Apply the `[Property]` attribute

Use the standard `[Property]` attribute from your test framework adapter (`Conjecture.Xunit`, `Conjecture.NUnit`, or `Conjecture.MSTest`):

```csharp
[Property]
public async Task OrdersRoundTripCorrectly(IAspireAppFixture fixture)
{
    // property body
}
```

The fixture is injected automatically when wired via xUnit class fixtures or equivalent.
