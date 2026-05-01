# Conjecture.Interactions.Abstractions

Interaction and state-machine contracts for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Reference this package when building a transport bridge (e.g. `Conjecture.Dapper`, `Conjecture.gRPC`). End-user test code should reference [`Conjecture.Interactions`](https://www.nuget.org/packages/Conjecture.Interactions) instead.

## Who is this for?

Authors implementing a new `IInteractionTarget` or wrapping `InteractionStateMachine<TState>` for a transport or protocol not already supported.

## Install

```
dotnet add package Conjecture.Interactions.Abstractions
```

## Types

| Type | Role |
|---|---|
| `IInteraction` | Marker interface for all interaction DTOs. |
| `IAddressedInteraction` | `IInteraction` with a `ResourceName` property for dispatch routing. |
| `IInteractionTarget` | Executes an interaction and returns a result. Implement this for each transport. |
| `InteractionStateMachine<TState>` | Abstract base for stateful property tests. Subclass and implement `InitialState`, `Commands`, `RunCommand`, and `Invariant`. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
