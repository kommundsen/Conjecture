# 0033. Stateful Test Entry Point API

**Date:** 2026-04-01
**Status:** Accepted

## Context

Stateful testing (ADR-0015) requires an entry point for users to declare a test that exercises an `IStateMachine<TState, TCommand>` implementation. The entry point must integrate with all four supported test frameworks (xUnit v2, xUnit v3, NUnit, MSTest) without duplicating framework adapter wiring, and must preserve existing infrastructure: seed reproduction via `[Property(Seed=...)]`, explicit `[Example]` cases, the example database, `MaxExamples`, and `Deadline` settings.

## Decision

Reuse the existing `[Property]` attribute with a `[From<StateMachineStrategy<TMachine,TState,TCommand>>]`-decorated parameter of type `StateMachineRun<TState>`. No new attribute types are introduced.

```csharp
[Property]
public void Stack_invariants_hold(
    [From<StateMachineStrategy<StackMachine, Stack<int>, StackCommand>>]
    StateMachineRun<Stack<int>> run)
    => _ = run; // invariant failures are reported automatically
```

`StateMachineStrategy<TMachine, TState, TCommand>` is `internal`. It extends `Strategy<StateMachineRun<TState>>` and implements `IStrategyProvider<StateMachineRun<TState>>`, making it usable as the type argument to `[From<T>]` via the existing `SharedParameterStrategyResolver`. The `TMachine` type parameter is constrained to `new()` so the engine instantiates it with `new TMachine()` — no reflection, NativeAOT-safe (ADR-0014).

`Gen.StateMachine<TMachine, TState, TCommand>(int maxSteps = 50)` is the public convenience factory on `Gen.cs` for use in composite strategies.

`StateMachineRun<TState>` is a public result type carrying:
- `Steps` — `IReadOnlyList<ExecutedStep<TState>>` in execution order
- `FailureStepIndex` — `int?`; null if all invariants passed, otherwise the 0-based index of the failing step
- `FinalState` — `TState`; state after the last executed step
- `Passed` — `bool`; true iff `FailureStepIndex` is null

## Consequences

- Zero new attribute types; all four framework adapters gain stateful testing support with no adapter-level changes.
- All existing `[Property]` infrastructure (seed reproduction, example database, `[Example]`, settings) applies to stateful tests without additional wiring.
- The user-facing declaration is more verbose than a hypothetical `[StateMachineTest<StackMachine>]` attribute, but this verbosity is consistent with how other custom strategies are declared via `[From<T>]`.
- `StateMachineStrategy` stays `internal`; the stable public surface is `IStateMachine<TState,TCommand>`, `StateMachineRun<TState>`, `ExecutedStep<TState>`, and `Gen.StateMachine<>()`.

## Alternatives Considered

- **New `[StateMachineProperty]` attribute:** More ergonomic declaration (`[StateMachineProperty<StackMachine>]`), but requires duplicating test-case discovery, execution, and parameter resolution logic across xUnit v2, xUnit v3, NUnit, and MSTest adapters. Rejected to avoid maintenance surface duplication.
- **Fluent builder (`StateMachine.For<StackMachine>().Run()`):** Discoverable via IntelliSense but introduces a non-obvious deferred-execution model that conflicts with the declarative `[Property]` pattern used throughout the library.
