# Draft: Roslyn Analyzer Expansion

## Motivation

Conjecture ships 6 Roslyn analyzers (CON100-CON105) bundled in `Conjecture.Core`. These catch common mistakes at compile time — a capability Python Hypothesis fundamentally cannot offer. Expanding the analyzer suite deepens this advantage and improves the developer experience by catching more issues before tests run.

## .NET Advantage

Roslyn's analyzer infrastructure runs in the IDE and at build time, giving Conjecture a channel to surface property test quality issues before tests execute. Mistakes like unreachable assumptions, high-rejection filters, non-deterministic operations, and mismatched strategy types can be flagged as diagnostics with code fixes — catching issues at the earliest possible point in the development workflow.

## Key Ideas

### New Diagnostic Rules

**CON106: High-Rejection Where Predicate**
Detect `.Where()` predicates that are statically analyzable as likely to reject most inputs:
```csharp
// Warning: predicate rejects >90% of int range
Generate.Integers<int>().Where(x => x == 42); // CON106
// Suggestion: use Generate.Just(42) or Generate.Integers<int>(42, 42)
```

**CON107: Non-Deterministic Operation in Property**
Detect calls to `Random`, `DateTime.Now`, `Guid.NewGuid()`, etc. inside `[Property]` methods:
```csharp
[Property]
public bool Prop(int x)
{
    var id = Guid.NewGuid(); // CON107: non-deterministic, breaks reproducibility
    return x + 1 > x;
}
```

**CON108: Unreachable Assume.That()**
Detect `Assume.That()` with conditions that are always true or always false based on parameter constraints:
```csharp
[Property]
public bool Prop([From<PositiveInts>] int x)
{
    Assume.That(x > 0); // CON108: always true given [From<PositiveInts>]
    return x * 2 > x;
}
```

**CON109: Missing Strategy for Parameter Type**
Detect `[Property]` method parameters that have no resolvable strategy:
```csharp
[Property]
public bool Prop(MyCustomType x) // CON109: no strategy found for MyCustomType
{
    return x != null;
}
// Suggestion: add [Arbitrary] to MyCustomType or use [From<MyCustomTypeProvider>]
```

**CON110: Async Property Without Await**
Detect `async` property methods that never `await`:
```csharp
[Property]
public async Task<bool> Prop(int x) // CON110: async without await
{
    return x > 0;
}
```

**CON111: Target.Maximize/Minimize Outside Property Context**
Detect `Target.Maximize()` or `Target.Minimize()` calls outside `[Property]` methods.

**CON112: Strategy Composition Type Mismatch**
Detect `.Select()` chains where the output type doesn't match the property parameter type.

### Code Fixes
- CON106 → Replace `.Where(x => x == value)` with `Generate.Just(value)`
- CON107 → Suggest injecting randomness via strategy parameter
- CON109 → Add `[Arbitrary]` attribute to type (if user-defined) or suggest `[From<T>]`
- CON110 → Remove `async` keyword

## Design Decisions to Make

1. How deep should static analysis go for CON106? Simple constant analysis or data-flow analysis?
2. Should CON107 (non-determinism) be a warning or info? Some users may intentionally use randomness.
3. How to handle CON108 when the always-true condition depends on strategy provider internals?
4. Severity levels: which should be errors vs warnings vs suggestions?
5. Should analyzers be configurable via `.editorconfig`?
6. Diagnostic ID range: CON106-CON1XX — reserve space for future additions

## Scope Estimate

Medium-Large. Each analyzer is independent and can be implemented incrementally. ~1 cycle per 2-3 analyzers.

## Dependencies

- Existing `Conjecture.Analyzers` project infrastructure
- Roslyn `IOperation` API for data-flow analysis
- `DiagnosticAnalyzer` and `CodeFixProvider` base classes
- `AnalyzerReleases.Unshipped.md` tracking

## Open Questions

- Which analyzers provide the most value? Prioritize by frequency of user mistakes.
- Should we provide a "strict mode" that treats all warnings as errors?
- Can we detect high-rejection predicates without actually running the predicate? (Static analysis limitations)
- How to test analyzers? Existing `Conjecture.Analyzers.Tests` uses Roslyn test infrastructure — extend it.
- Should we publish an analyzer cookbook for users who want to write custom Conjecture analyzers?
