# Draft: Union Type Strategies

## Motivation

C# 15 (previewing in .NET 11) introduces `union` types — discriminated unions with exhaustive pattern matching. This is a fundamentally new type category in .NET. A source generator can discover union case types and automatically compose strategies, making property-based testing of union-typed code effortless.

## .NET Advantage

C# 15's `union` keyword introduces discriminated unions as a first-class language construct with compiler-enforced exhaustiveness. Because union case types are visible to Roslyn at compile time, a source generator can auto-discover all cases and compose strategies automatically — no manual enumeration required. The type system guarantees completeness.

## Key Ideas

### Auto-Generated Union Strategies
```csharp
// User defines:
public union Shape(Circle, Square, Triangle);

// Source generator emits:
public class ShapeStrategy : IStrategyProvider<Shape>
{
    public Strategy<Shape> GetStrategy() =>
        Generate.OneOf(
            Generate.From<CircleArbitrary>().Select(c => (Shape)c),
            Generate.From<SquareArbitrary>().Select(s => (Shape)s),
            Generate.From<TriangleArbitrary>().Select(t => (Shape)t)
        );
}
```

### Strategy Resolution
- `[Arbitrary]` on a union type triggers case-type discovery
- Each case type must have its own strategy (via `[Arbitrary]`, `IStrategyProvider<T>`, or built-in type)
- Generator emits `OneOf` composition with equal weighting by default
- User can override weighting via `[UnionWeight(typeof(Circle), 0.5)]` attribute

### Union-Aware Shrinking
- Shrink toward the first case type (by declaration order)
- Within a case, shrink the case type's fields normally
- Cross-case shrinking: try replacing one case with another if the property still fails

### Exhaustive Case Coverage
- Analyzer warns if a property test only exercises some union cases
- `Generate.AllCasesOf<Shape>()` — ensure all cases are covered in a single test run

## Design Decisions to Make

1. When to implement? Union types are C# 15 preview — not stable yet. Wait for GA or prepare early?
2. How to handle `union` types that include generic case types?
3. Weighting strategy: equal weight, proportional to case complexity, or user-configurable?
4. Source generator must detect `union` keyword — requires Roslyn understanding of C# 15 syntax
5. Ship as part of `Conjecture.Generators` or a separate `Conjecture.Unions` package?
6. How to handle `IUnion` interface and `UnionAttribute` (runtime types not yet shipped in early .NET 11 previews)?

## Scope Estimate

Medium. Source generator work for union discovery + strategy composition. Blocked on C# 15 stabilization. ~2 cycles once union types are stable.

## Dependencies

- C# 15 compiler with `union` keyword support
- .NET 11 runtime with `UnionAttribute` and `IUnion` interface
- Existing `Conjecture.Generators` incremental source generator
- `Generate.OneOf()` combinator

## Open Questions

- Will union types support generic type parameters? (e.g., `union Result<T>(Success<T>, Error)`)
- How do union types interact with `[Arbitrary]` attribute on case types?
- Should Conjecture provide a polyfill for pre-.NET 11 "poor man's unions" (e.g., `OneOf<A, B, C>` libraries)?
- What does the Roslyn `INamedTypeSymbol` look like for union types? (Need to investigate once preview stabilizes)
