# Conjecture Stateful Testing API Reference

## `IStateMachine<TState, TCommand>`

```csharp
public interface IStateMachine<TState, TCommand>
{
    /// Returns the initial state before any commands run.
    TState InitialState();

    /// Returns the strategies for commands valid in the given state.
    /// Return an empty list to stop command generation.
    IEnumerable<Strategy<TCommand>> Commands(TState state);

    /// Applies a command to the state and returns the next state.
    TState RunCommand(TState state, TCommand command);

    /// Called after each command. Throw to signal an invariant violation.
    void Invariant(TState state);
}
```

## `Generate.StateMachine<TMachine, TState, TCommand>(maxSteps)`

```csharp
// TMachine must implement IStateMachine<TState, TCommand> and have a public parameterless ctor.
Strategy<StateMachineRun<TState>> strategy =
    Generate.StateMachine<MyMachine, MyState, MyCommand>(maxSteps: 50);
```

## `StateMachineRun<TState>`

```csharp
public sealed record class StateMachineRun<TState>
{
    public IReadOnlyList<ExecutedStep<TState>> Steps { get; }
    public int? FailureStepIndex { get; }
    public TState FinalState { get; }
    public bool Passed { get; }
}

public record struct ExecutedStep<TState>
{
    public TState State { get; }
    public string CommandLabel { get; }
}
```

## Complete Example

```csharp
// The model: a simple stack
public sealed class StackMachine : IStateMachine<Stack<int>, string>
{
    private readonly Stack<int> _model = new();

    public Stack<int> InitialState() => new();

    public IEnumerable<Strategy<string>> Commands(Stack<int> state)
    {
        yield return Generate.Integers<int>().Select(n => $"push:{n}");
        if (state.Count > 0)
            yield return Generate.Just("pop");
    }

    public Stack<int> RunCommand(Stack<int> state, string command)
    {
        var next = new Stack<int>(state.Reverse());
        if (command.StartsWith("push:"))
            next.Push(int.Parse(command[5..]));
        else
            next.Pop();
        return next;
    }

    public void Invariant(Stack<int> state)
    {
        // model and actual system should agree
    }
}

// The test
[Property]
public void Stack_BehavesLikeModel(StateMachineRun<Stack<int>> run)
{
    // The strategy generates and executes the command sequence.
    // Conjecture shrinks failing sequences to minimal counterexamples.
    Assert.True(run.Passed, $"Invariant violated at step {run.FailureStepIndex}");
}
```

## Shrinking

When a command sequence causes `Invariant` to throw, Conjecture shrinks it by:
1. Truncating from the end (halving)
2. Deleting individual commands
3. Simplifying command values

The shrunk sequence in the counterexample output is the minimal sequence that still triggers the bug.
