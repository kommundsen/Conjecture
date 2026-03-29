# 0028. Parameter Strategy Resolution Attributes

**Date:** 2026-03-29
**Status:** Accepted

## Context

Phase 0–1 `ParameterStrategyResolver` infers a strategy for each `[Property]` parameter purely by its CLR type (`int` → `Gen.Integers<int>()`, `string` → `Gen.Strings()`, etc.). This works for built-in types but offers no way for users to:

1. **Constrain generation** — e.g. positive integers only, short strings, domain-specific ranges.
2. **Reuse custom strategies** — a `PositiveInts` strategy shared across many test methods.
3. **Supply one-off strategies** — an inline factory method for a test-local constraint.
4. **Run explicit examples** — known edge cases executed before random generation.

Users cannot subclass `Strategy<T>` directly because `Next(ConjectureData)` is `internal abstract` and `ConjectureData` is internal. A public extension point is needed that composes from the existing `Gen.*` API without exposing engine internals.

C# 11+ generic attributes (available on .NET 10 / C# 14, ADR-0006) enable compile-time-safe parameter annotation: `[From<PositiveInts>]` rather than `[From(typeof(PositiveInts))]`.

## Decision

Introduce four public types in `Conjecture.Core` (framework-agnostic, usable by future NUnit/MSTest adapters):

### 1. `IStrategyProvider<out T>`

```csharp
public interface IStrategyProvider<out T>
{
    Strategy<T> Create();
}
```

Users implement this to define reusable strategy types:

```csharp
public sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Gen.Integers(min: 1);
}
```

The interface is covariant (`out T`) so `IStrategyProvider<string>` is assignable to `IStrategyProvider<object>`.

### 2. `FromAttribute<TProvider>`

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromAttribute<TProvider> : Attribute
    where TProvider : IStrategyProvider, new()
{
}
```

A non-generic marker interface `IStrategyProvider` (no members) is the attribute constraint — C# generic attribute constraints cannot reference the parameter's type `T`. The resolver performs a runtime check that the provider implements `IStrategyProvider<T>` for the annotated parameter's type.

Usage:

```csharp
[Property]
public void Test([From<PositiveInts>] int x) => Assert.True(x > 0);
```

### 3. `FromFactoryAttribute`

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromFactoryAttribute(string methodName) : Attribute
{
    public string MethodName { get; } = methodName;
}
```

References a `static` method on the test class that returns `Strategy<T>`:

```csharp
[Property]
public void Test([FromFactory(nameof(EvenInts))] int n) => Assert.Equal(0, n % 2);

static Strategy<int> EvenInts() => Gen.Integers(0, 1000).Where(n => n % 2 == 0);
```

The resolver finds the method via reflection (`BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic`), validates its return type matches `Strategy<T>` for the parameter type, and invokes it once per test run (not per example).

### 4. `ExampleAttribute`

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExampleAttribute(params object?[] arguments) : Attribute
{
    public object?[] Arguments { get; } = arguments;
}
```

Runs explicit test cases *before* random generation:

```csharp
[Property]
[Example(0, 0)]
[Example(int.MaxValue, 1)]
public void Add_commutes(int a, int b) => Assert.Equal(a + b, b + a);
```

Explicit examples are not shrunk on failure (the user supplied them deliberately). They contribute to `ExampleCount` in reporting. Argument count mismatch throws `InvalidOperationException` at test start.

### Resolution Order

`ParameterStrategyResolver` checks each parameter in this order:

1. **`[From<TProvider>]`** — instantiate `TProvider`, cast to `IStrategyProvider<T>`, call `Create()`.
2. **`[FromFactory("name")]`** — reflect to find static method, invoke, use returned `Strategy<T>`.
3. **Type inference** — existing Phase 1 type-switch (`int` → `Gen.Integers<int>()`, etc.).
4. **Unsupported** — throw `NotSupportedException` with message suggesting `[From<T>]`.

Steps 1–2 are checked per-parameter; step 3 is the fallback. A parameter may have at most one of `[From<T>]` or `[FromFactory]`; both present throws `InvalidOperationException`.

### Attribute Placement

All four types live in `Conjecture.Core`, not `Conjecture.Xunit`. This makes them framework-agnostic: future NUnit/MSTest adapters read the same attributes without depending on xUnit. The resolver logic that interprets them lives in `Conjecture.Xunit.Internal.ParameterStrategyResolver` (and future adapter equivalents).

## Consequences

- Users can constrain generation without touching engine internals — `IStrategyProvider<T>` composes from `Gen.*` which is the existing public API.
- `[From<T>]` is compile-time type-safe at the provider level; the `T`-vs-parameter-type check is runtime (unavoidable given C# attribute constraints).
- `[FromFactory]` uses reflection (string-based method name), making it less safe than `[From<T>]` but more convenient for one-off strategies. Phase 3 Roslyn analyzer CON105 (ADR-0023) can add compile-time validation.
- `[Example]` uses `params object?[]`, limiting arguments to attribute-legal types (primitives, strings, enums, `Type`, arrays thereof). Complex types require `[From<T>]` or `[FromFactory]`.
- The non-generic `IStrategyProvider` marker interface is a minor API wart forced by C# generic attribute constraints. It has no members and exists solely for the `where TProvider : IStrategyProvider` bound.
- `ExampleAttribute` on `Conjecture.Core` means the core package has no test-framework dependency — explicit examples are a core concept, not xUnit-specific.

## Alternatives Considered

- **Public base classes (`NumberStrategy<T>`, `StringStrategy`, etc.)**: Users subclass concrete strategy types with constructor parameters. Ergonomic for the specific base class, but requires one base class per strategy category and still can't expose `Next(ConjectureData)` to user assemblies. Rejected because `IStrategyProvider<T>` is a single interface that works for all strategies uniformly.
- **`[From(typeof(PositiveInts))]` non-generic attribute**: Works on older C# versions but loses IntelliSense and compile-time type checking. Since we target .NET 10 (ADR-0006), generic attributes are available. Rejected.
- **`Func<Strategy<T>>` parameter on `[Property]`**: Attributes cannot accept delegate values. Rejected.
- **Convention-based discovery (static `Strategy` property on provider)**: No interface, just a naming convention. Fragile, hard to discover, no compiler enforcement. Rejected.
- **`[Example]` as separate test cases (like xUnit `[InlineData]`)**: Would make each example a distinct test in the runner. Rejected because explicit examples should run as part of the property test lifecycle (before generation, contributing to the same pass/fail result).
