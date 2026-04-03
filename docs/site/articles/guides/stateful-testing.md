# Stateful Testing

Property tests work well for pure functions, but many real systems are stateful: a stack, a queue, a cache, a domain aggregate. For these, the interesting bugs don't appear from a single input — they emerge from *sequences of operations*.

Conjecture's stateful testing engine lets you model a system as a state machine and then automatically explore command sequences, verifying that an invariant holds after every step.

## When to Use Stateful Testing

Use `Generate.StateMachine<>` when:

- Your system is **stateful** and correctness depends on the order of operations.
- You want to verify a **safety invariant** holds across all reachable states.
- Bugs arise from *interaction between commands*, not from any single input.

For pure functions (sorting, parsing, encoding), a regular `[Property]` test is simpler and faster.

## Core Concepts

| Term | Meaning |
|---|---|
| **State** | A snapshot of the system at a point in time. Must be immutable (return a new state from `RunCommand`). |
| **Command** | A single operation that transitions one state to another. |
| **Invariant** | An assertion that must hold after every command. Throw to signal a violation. |

## Implementing `IStateMachine<TState, TCommand>`

### 1. Define state and commands

```csharp
// Immutable state snapshot — track both the real stack and a model count
public readonly record struct StackState(Stack<int> Items, int ModelCount);

// Commands: a discriminated union represented as an abstract class hierarchy
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
    // Starting state: empty stack, model count of zero
    public StackState InitialState() => new(new Stack<int>(), ModelCount: 0);

    // Both commands are always available (Pop on empty is treated as a no-op)
    public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
    {
        yield return Generate.Integers<int>(0, 9)
            .Select(n => (StackCommand)new StackCommand.Push(n));
        yield return Generate.Just((StackCommand)new StackCommand.Pop());
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
            return state; // Pop on empty stack is a no-op

        var popped = new Stack<int>(state.Items.Reverse());
        popped.Pop();
        return new(popped, state.ModelCount - 1);
    }

    // The invariant: our model count must always equal the real stack depth
    public void Invariant(StackState state)
    {
        if (state.ModelCount != state.Items.Count)
            throw new InvalidOperationException(
                $"ModelCount {state.ModelCount} != Items.Count {state.Items.Count}");
    }
}
```

### 3. Write the property test

Expose the machine as an `IStrategyProvider` and use `[From<T>]`:

```csharp
public sealed class StackMachineProvider : IStrategyProvider<StateMachineRun<StackState>>
{
    public Strategy<StateMachineRun<StackState>> Create()
        => Generate.StateMachine<StackMachine, StackState, StackCommand>(maxSteps: 100);
}

[Property]
public void Stack_InvariantHoldsForAllCommandSequences(
    [From<StackMachineProvider>] StateMachineRun<StackState> run)
{
    // The engine verified Invariant(state) after every command.
    // If execution reaches this line, the entire sequence passed.
    // Add extra post-run assertions here if needed.
}
```

The engine generates random command sequences (up to `maxSteps` steps), calls `RunCommand` for each command, and asserts `Invariant` after every step. The test fails the moment any sequence violates the invariant.

## Annotated Failure Output

When an invariant violation is found and shrunk to a minimal sequence, the counterexample shows the exact execution:

```
Falsified after 12 examples (7 shrinks, seed: 1234567890).
Counterexample:
  run =
    state = InitialState();
    RunCommand(state, Push(0));
    RunCommand(state, Pop);
    Invariant(state); // ← fails here
```

Each line is one step. The `// ← fails here` marker identifies the command whose post-state violated the invariant. If you planted a bug in `Pop` that decrements `ModelCount` by 2, the shrinker finds that `[Push(0), Pop]` is the minimal reproducer — one push to make the stack non-empty, one buggy pop to trigger the violation.

## State-Dependent Command Availability

`Commands(TState state)` receives the current state, so you can restrict which commands are valid at any point. This prevents the engine from generating nonsensical sequences and makes the shrunk output more precise:

```csharp
public IEnumerable<Strategy<StackCommand>> Commands(StackState state)
{
    // Push is always available
    yield return Generate.Integers<int>(0, 9)
        .Select(n => (StackCommand)new StackCommand.Push(n));

    // Pop is only offered when the stack is non-empty
    if (state.Items.Count > 0)
        yield return Generate.Just((StackCommand)new StackCommand.Pop());
}
```

If `Commands` returns an empty sequence at any point, the engine stops generating further steps for that run and moves on.

> **Tip:** State-dependent `Commands` also prevents the engine from accidentally triggering unrelated errors (like `InvalidOperationException` from popping an empty stack), which would obscure the real invariant violation you're testing for.

## Shrinking

When a failure is found, Conjecture's shrinker reduces the sequence to its minimal form:

1. **Delete commands** — removes individual commands or contiguous blocks and replays the sequence to verify the invariant still fails.
2. **Simplify command values** — reduces integer arguments, strings, etc., toward simpler values.
3. **Repeat** — continues until no further reduction is possible.

For the stack example, a sequence like `[Push(7), Push(3), Push(1), Pop, Push(5), Pop]` shrinks to `[Push(0), Pop]` — the smallest sequence that exposes the bug.

The key insight: the shrinker operates on the byte buffer backing the generated run, not on the sequence directly. This means it can delete arbitrary subsequences in a single pass, making it fast even for long sequences.

## Seed Reproduction

Every failure is tied to a seed, shown in the failure message. Reproduce it deterministically using `[ConjectureSettings]`:

```csharp
[Property]
[ConjectureSettings(Seed = 1234567890UL)]
public void Stack_InvariantHoldsForAllCommandSequences(
    [From<StackMachineProvider>] StateMachineRun<StackState> run)
{
    // Replays the exact same sequence every run.
}
```

Pin the seed while debugging. Remove it once the bug is fixed so the property resumes random exploration.
