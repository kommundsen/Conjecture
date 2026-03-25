# 0015. Stateful Testing Model

**Date:** 2026-03-25
**Status:** Accepted

## Context

Beyond pure property tests (stateless input → output), Hypothesis supports stateful testing: generating sequences of commands against a stateful system under test and verifying invariants hold throughout. The design must choose an abstraction for expressing state machines and command sequences.

## Decision

Model stateful tests via an `IStateMachine<TState, TCommand>` interface. Users implement `InitialState()`, `Commands(TState)` (returns available commands given current state), `RunCommand(TState, TCommand)` (returns next state), and `Invariant(TState)`. The engine generates and shrinks command sequences automatically.

## Consequences

- The interface is strongly typed: state and command types are explicit, enabling the source generator to derive shrinking for command sequences.
- Invariant checking is separated from command execution, making it easy to assert postconditions after every step.
- The `Commands(TState)` method allows state-dependent command availability, matching Python Hypothesis's `RuleBasedStateMachine` pattern.
- Users must implement an interface rather than using attribute-decorated methods (Python's style); this is more idiomatic for C# but slightly more verbose.
- Shrinking of command sequences (finding the minimal sequence that triggers the invariant violation) requires special shrinker support beyond primitive shrinking.

## Alternatives Considered

- **Attribute-decorated rule methods (Python-style)**: Closer to the Python API; uses reflection to discover rules at runtime. Incompatible with NativeAOT (ADR-0014) and harder to make source-generator-friendly.
- **Fluent builder API**: `StateMachine.WithCommand(...).WithInvariant(...)`. More discoverable but less structured; harder to type-check command applicability.
