# Understanding sealed hierarchy strategies

C# developers model discriminated union-like types with a sealed abstract base class and a closed set of concrete subtypes. Conjecture's source generator recognizes this pattern and derives a `Generate.OneOf` strategy automatically.

## The pattern

Before native union types arrived in C#, the idiomatic way to represent a fixed set of alternatives was:

```csharp
public abstract class Shape { }
public class Circle : Shape { }
public class Rectangle : Shape { }
```

Pattern matching over such a hierarchy is exhaustive when the hierarchy is sealed — the compiler can verify you've handled every case. C# 14 formalized this with sealed class modifiers and `switch` exhaustiveness checks.

When generating `Shape` values for property tests you want every subtype represented, not just one. `Generate.OneOf` is the right combinator: it picks uniformly among a set of strategies. Writing this by hand is mechanical repetition — exactly what a source generator should eliminate.

## How the generator works

When `[Arbitrary]` is applied to an abstract class, the generator switches to hierarchy mode:

1. It walks the entire compilation looking for concrete classes that inherit (directly or indirectly) from the base.
2. For each concrete subtype decorated with `[Arbitrary]`, it collects the subtype's own generated `IStrategyProvider<T>`.
3. It emits a provider for the base type that calls `Generate.OneOf` over all the collected subtype strategies, casting each result up to the base type.

The output for `Shape` / `Circle` / `Rectangle` looks like:

```csharp
public sealed class ShapeArbitrary : IStrategyProvider<Shape>
{
    public Strategy<Shape> Create() =>
        Generate.OneOf(
            new CircleArbitrary().Create().Select(static x => (Shape)x),
            new RectangleArbitrary().Create().Select(static x => (Shape)x)
        );
}
```

Each subtype appears with equal probability. Shrinking works correctly because `Generate.OneOf` delegates shrinking to whichever branch was chosen.

## The same-compilation constraint

The generator only sees types in the current compilation. Subtypes defined in a referenced assembly are invisible to Roslyn's incremental generator pipeline — they are not part of the syntax tree being processed.

This is intentional, not a limitation to work around. A hierarchy that spans assemblies is not a closed set from the generator's perspective: there is no way to enumerate all subtypes statically. The pattern is most useful precisely when the set of cases is closed and known at compile time.

If you control an external subtype assembly and want it included, move the subtype into the same project, or define a manual `IStrategyProvider<Base>` implementation that assembles the `OneOf` by hand.

Concrete subtypes that exist in the compilation but lack `[Arbitrary]` trigger a **CON205** warning so you don't silently miss a case.

## Relationship to C# 15 union types

C# 15 is expected to introduce a first-class `union` keyword that eliminates the sealed-abstract-class boilerplate and gives the compiler direct knowledge of the case set. Conjecture tracks this in [issue #79](https://github.com/kommundsen/Conjecture/issues/79).

The current `[Arbitrary]`-on-abstract-base pattern is designed to migrate cleanly: the generator already treats the abstract base as a discriminated union. When native unions land, the plan is to recognize the `union` syntax directly without requiring `[Arbitrary]` on each case — the decorated-abstract-class form will remain supported for backward compatibility.

## Further reading

- [How to generate sealed class hierarchies](../how-to/use-sealed-hierarchy-strategies.md)
- [Reference: Analyzers — CON205, CON300–CON302](../reference/analyzers.md#source-generator-diagnostics)
- [How to use source generators](../how-to/use-source-generators.md) — the concrete-type path
