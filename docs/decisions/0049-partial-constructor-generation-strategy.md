# 0049. Partial Constructor Generation Strategy

**Date:** 2026-04-16
**Status:** Accepted

## Context

The `[Arbitrary]` source generator (ADR-0029) emits `{TypeName}Arbitrary` implementations that call `Strategies.Compose` and inject drawn values via an `IGeneratorContext` parameter. This works for all supported shapes — primary constructors, single public constructors, init-only properties — because the generator controls the call site and can pass the context explicitly.

However, some types have constructors that set private fields directly, apply invariants, or do non-trivial initialization work that the generator cannot replicate from the outside. These types benefit from *providing their own constructor body* that draws from Conjecture strategies. C# partial methods/constructors let the user declare the signature; a generator provides the implementation. The challenge is that constructor signatures are fixed: you cannot add an `IGeneratorContext` parameter without exposing it as part of the public API. An ambient mechanism is required.

The existing `{TypeName}Arbitrary` pattern remains the primary generation path. Partial constructors are an opt-in supplement for types where constructor-internal access is necessary.

## Decision

### Ambient context via `PartialConstructorContext`

Introduce a static `PartialConstructorContext` class in `Conjecture.Generators` that wraps an `AsyncLocal<IGeneratorContext>`:

```csharp
public static class PartialConstructorContext
{
    private static readonly AsyncLocal<IGeneratorContext?> _current = new();

    public static IGeneratorContext Current =>
        _current.Value ?? throw new InvalidOperationException(
            "PartialConstructorContext.Current accessed outside of a Conjecture test.");

    internal static IDisposable Set(IGeneratorContext ctx) { ... }
}
```

The emitter sets `PartialConstructorContext` before invoking the partial constructor and clears it afterwards. The partial constructor body — provided by the generator — calls `PartialConstructorContext.Current.Next(...)` to draw values:

```csharp
// User writes:
[Arbitrary]
public partial class Order
{
    public partial Order(CustomerId customerId, decimal amount);
}

// Generator emits:
public partial class Order
{
    public partial Order(CustomerId customerId, decimal amount)
    {
        customerId = PartialConstructorContext.Current.Next(new CustomerIdArbitrary().Create());
        amount = PartialConstructorContext.Current.Next(Gen.Decimals(0m, 10_000m));
    }
}
```

The `AsyncLocal` is safe under parallel test execution: each async execution context has its own slot. The performance cost of an `AsyncLocal` lookup is a single dictionary read per constructor invocation, which is acceptable for test-only construction (not production hot paths).

### Opt-in selection rule

A type decorated with `[Arbitrary]` that also declares exactly one `partial` constructor activates partial constructor generation. The generator emits the constructor body instead of (or in addition to) a `{TypeName}Arbitrary` class, depending on whether other init members are present.

### Diagnostics

| ID | Severity | Condition |
|---|---|---|
| CON203 | Error | Multiple `partial` constructor declarations on the same `[Arbitrary]` type — ambiguous; cannot select one |
| CON204 | Error | Primary constructor combined with a `partial` constructor on the same `[Arbitrary]` type — unsupported combination; use the standard generation path |

Unimplemented `partial` constructors on types not decorated with `[Arbitrary]` are left entirely to the compiler (CS8795). Conjecture does not emit warnings for code it does not own.

### Relationship to `{TypeName}Arbitrary`

Partial constructor generation supplements, not replaces, the `{TypeName}Arbitrary` pattern. Both may coexist in a project. The partial constructor path is chosen only when the user explicitly declares a `partial` constructor on an `[Arbitrary]` type.

## Consequences

- Types with private fields or invariant-enforcing constructors become testable without exposing extra parameters.
- `AsyncLocal` is the only viable ambient mechanism for constructor injection in C#; the cost is a dictionary lookup per constructor call, acceptable in test context.
- Requiring an explicit `partial` constructor declaration keeps the feature opt-in and auditable — the user's code always shows intent.
- CON203 and CON204 prevent silent selection errors when type shapes are ambiguous or unsupported.
- Unimplemented partials on non-`[Arbitrary]` types produce CS8795 from the compiler, which is the correct owner of that error.

## Alternatives Considered

- **Explicit `IGeneratorContext` parameter**: Would require changing the constructor signature, leaking a test-infrastructure type into the production API. Rejected.
- **Thread-static field**: Not safe under `async`/`await` or parallel test execution. Rejected in favour of `AsyncLocal`.
- **Constructor interception via source generator rewriting**: Rewriting user-provided constructor bodies is fragile and conflicts with Roslyn's incremental generator model. Rejected.
- **Always use `{TypeName}Arbitrary`**: Works for external construction but cannot access private fields or run invariant logic mid-construction. Partial constructors fill this gap for opt-in cases.
