# Draft: Extension Member Strategy DSL

## Motivation

C# 14 introduces `extension` blocks supporting extension properties, static extension methods, and extension operators. This enables a richer, more discoverable strategy composition API where common transformations appear as properties or methods directly on `Strategy<T>` without polluting the base class.

## .NET Advantage

C# 14's `extension` blocks allow adding properties, methods, and operators to existing types without modifying them. This means strategy objects gain IntelliSense-discoverable composition — users can explore available transformations directly on a `Strategy<T>` instance via autocomplete, rather than needing to know function names upfront.

## Key Ideas

```csharp
// Instance extension properties
extension(Strategy<int> s)
{
    public Strategy<int> Positive => s.Where(x => x > 0);
    public Strategy<int> Negative => s.Where(x => x < 0);
    public Strategy<int> NonZero => s.Where(x => x != 0);
}

extension(Strategy<string> s)
{
    public Strategy<string> NonEmpty => s.Where(x => x.Length > 0);
    public Strategy<string> Alphabetic => s.Where(x => x.All(char.IsLetter));
    public Strategy<string> Numeric => s.Where(x => x.All(char.IsDigit));
}

extension(Strategy<IList<T>> s)
{
    public Strategy<IList<T>> NonEmpty => s.Where(x => x.Count > 0);
}

// Static extension members on Generate
extension(Generate)
{
    public static Strategy<T> Constant<T>(T value) => Generate.Just(value);
}
```

- Ship as extension methods in `Conjecture.Core` or a separate `Conjecture.Extensions` package
- Users can define their own domain-specific extensions using the same pattern
- Document the pattern in the user guide as a recommended practice

## Design Decisions to Make

1. Ship in Core or separate package? Core means everyone gets them; separate means opt-in.
2. Which extension properties are "blessed" vs left to users?
3. Naming conventions: `.Positive` vs `.WherePositive()` vs `.ConstrainedTo(x => x > 0)`?
4. How to avoid filter-budget issues with extension properties that use `.Where()`? Should some use targeted strategies instead?
5. Extension operators: `strategy1 | strategy2` for `OneOf`?

## Scope Estimate

Small. Mostly API surface design + documentation. ~1 cycle for core extensions, ongoing for domain-specific ones.

## Dependencies

- C# 14 compiler (ships with .NET 10 SDK)
- Existing `Strategy<T>` base class and LINQ combinators

## Open Questions

- Do extension properties compose well with LINQ query syntax?
- Should we provide a Roslyn analyzer that suggests extension properties when it detects common `.Where()` patterns?
- How do extension members interact with the source generator's `[Arbitrary]` output?
