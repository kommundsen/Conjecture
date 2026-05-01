# How to test stateful systems

Property tests work well for pure functions, but many real systems are stateful: a stack, a queue, a cache, a domain aggregate. For these, the interesting bugs emerge from *sequences of operations*, not from any single input.

Conjecture's stateful testing engine models your system as a state machine and automatically explores command sequences, verifying that an invariant holds after every step.

> [!NOTE]
> For background on why this approach finds bugs that unit tests miss, see [Understanding targeted and stateful testing](../explanation/targeted-testing.md).

## When to use stateful testing

Use `Strategy.StateMachine<>` when:

- Your system is **stateful** and correctness depends on the order of operations.
- You want to verify a **safety invariant** holds across all reachable states.
- Bugs arise from *interaction between commands*, not from any single input.

For pure functions (sorting, parsing, encoding), a regular `[Property]` test is simpler and faster.

## Implement `IStateMachine<TState, TCommand>`

### 1. Define state and commands

```csharp
// Immutable state snapshot
public readonly record struct StackState(Stack<int> Items, int ModelCount);

// Commands: abstract class hierarchy
public abstract class StackCommand
{
    public sealed class Push(int value) : StackCommand
    {
        public int Value { get; } = value;
        public override string ToString() => $"Push({Value})";
    }

    public sealed class Pop : StackCommand
    {
        public override string ToString() => "Pop";
    }
}
```

### 2. Implement the machine

```csharp
public sealed class StackMachine : IStateMachine<StackState, StackCommand>
{
    public StackState InitialState() => new(new Stack<int>(), ModelCount: 0);

    public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
    {
        yield return Strategy.Integers<int>(0, 9)
            .Select(n => (StackCommand)new StackCommand.Push(n));
        yield return Strategy.Just((StackCommand)new StackCommand.Pop());
    }

    public StackState RunCommand(StackState state, StackCommand cmd)
    {
        if (cmd is StackCommand.Push push)
        {
            var items = new Stack<int>(state.Items.Reverse());
            items.Push(push.Value);
            return new(items, state.ModelCount + 1);
        }

        if (state.Items.Count == 0)
        {
            return state; // Pop on empty stack is a no-op
        }

        var popped = new Stack<int>(state.Items.Reverse());
        popped.Pop();
        return new(popped, state.ModelCount - 1);
    }

    public void Invariant(StackState state)
    {
        if (state.ModelCount != state.Items.Count)
        {
            throw new InvalidOperationException(
                $"ModelCount {state.ModelCount} != Items.Count {state.Items.Count}");
        }
    }
}
```

### 3. Write the property test

Expose the machine as an `IStrategyProvider<T>` and use `[From<T>]`:

```csharp
public sealed class StackMachineProvider : IStrategyProvider<StateMachineRun<StackState>>
{
    public Strategy<StateMachineRun<StackState>> Create()
        => Strategy.StateMachine<StackMachine, StackState, StackCommand>(maxSteps: 100);
}

[Property]
public void Stack_InvariantHoldsForAllCommandSequences(
    [From<StackMachineProvider>] StateMachineRun<StackState> run)
{
    // The engine verified Invariant(state) after every command.
    // Add extra post-run assertions here if needed.
}
```

The engine generates random command sequences (up to `maxSteps`), calls `RunCommand` for each command, and asserts `Invariant` after every step.

## Restrict commands based on state

`Commands(TState state)` receives the current state, so you can prevent commands that don't apply:

```csharp
public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
{
    // Push is always available
    yield return Strategy.Integers<int>(0, 9)
        .Select(n => (StackCommand)new StackCommand.Push(n));

    // Pop is only offered when the stack is non-empty
    if (state.Items.Count > 0)
    {
        yield return Strategy.Just((StackCommand)new StackCommand.Pop());
    }
}
```

State-dependent `Commands` also prevents unrelated errors (like `InvalidOperationException` from popping an empty stack) from obscuring the real invariant violation you're testing for.

If `Commands` returns an empty sequence, the engine stops generating further steps and moves on to the next run.

## Read the failure output

When an invariant violation is found and shrunk to a minimal sequence, the counterexample shows the exact execution:

```text
Falsified after 12 examples (7 shrinks, seed: 1234567890).
Counterexample:
  run =
    state = InitialState();
    RunCommand(state, Push(0));
    RunCommand(state, Pop);
    Invariant(state); // ← fails here
```

Each line is one step. The `// ← fails here` marker identifies the command whose post-state violated the invariant.

## Reproduce a specific sequence

Every failure is tied to a seed, shown in the failure message. Pin it to replay deterministically:

```csharp
[Property]
[ConjectureSettings(Seed = 1234567890UL)]
public void Stack_InvariantHoldsForAllCommandSequences(
    [From<StackMachineProvider>] StateMachineRun<StackState> run)
{
    // Replays the exact same sequence every run.
}
```

Remove the seed once the bug is fixed so the property resumes random exploration.
