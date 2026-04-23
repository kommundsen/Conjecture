# Conjecture.Interactions

Transport-agnostic interaction abstractions for [Conjecture.NET](https://github.com/kommundsen/Conjecture).

## Interfaces

- `IInteraction` — marker interface for all interaction messages.
- `IInteractionTarget` — executes an `IInteraction` and returns an optional `object?` result.
