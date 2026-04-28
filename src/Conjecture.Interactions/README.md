# Conjecture.Interactions

Transport-agnostic interaction abstractions for [Conjecture](https://github.com/kommundsen/Conjecture). Provides the building blocks (`IInteraction`, `IInteractionTarget`, `InteractionStateMachine<TState>`) that satellite packages — `Conjecture.Http`, `Conjecture.Grpc`, `Conjecture.Messaging`, `Conjecture.EFCore` — implement, so a single property test can drive HTTP + DB + queue side effects through one model.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Interactions
```

You usually depend on this package transitively via one of the satellite packages. Use it directly when modeling a custom transport.

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Interactions;

public sealed record CounterState(int Value);

public sealed record IncrementInteraction(int By) : IInteraction;

public sealed class Counter : IInteractionTarget
{
    public int Value;

    public Task<object?> ExecuteAsync(IInteraction interaction, CancellationToken ct)
    {
        if (interaction is IncrementInteraction inc)
        {
            this.Value += inc.By;
        }
        return Task.FromResult<object?>(this.Value);
    }
}

public sealed class CounterMachine : InteractionStateMachine<CounterState>
{
    public override CounterState InitialState() => new(0);

    public override IEnumerable<Strategy<IInteraction>> Commands(CounterState state)
    {
        yield return Generate.Integers<int>(1, 10).Select(by => (IInteraction)new IncrementInteraction(by));
    }

    public override CounterState RunCommand(CounterState state, IInteraction interaction, IInteractionTarget target, CancellationToken ct)
    {
        IncrementInteraction inc = (IncrementInteraction)interaction;
        return state with { Value = state.Value + inc.By };
    }

    public override void Invariant(CounterState state)
    {
        if (state.Value < 0) { throw new InvalidOperationException("counter went negative"); }
    }
}
```

Then run with:

```csharp
await Property.ForAll(new Counter(), new CounterMachine());
```

## Types

| Type | Role |
|---|---|
| `IInteraction` | Marker interface for interaction messages. |
| `IAddressedInteraction` | Interaction tied to a named resource. |
| `IInteractionTarget` | Executes an interaction and returns an optional result. |
| `CompositeInteractionTarget` | Routes interactions to multiple named targets. |
| `InteractionStateMachine<TState>` | Stateful test model with `Commands`, `RunCommand`, `Invariant`. |
| `Property.ForAll(...)` | Runs a property or a state machine with shrinking. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
