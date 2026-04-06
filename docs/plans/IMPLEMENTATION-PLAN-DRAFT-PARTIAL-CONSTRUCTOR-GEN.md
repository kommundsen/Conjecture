# Draft: Partial Constructor Source Generation

## Motivation

C# 14 adds `partial` constructors, complementing partial methods and properties from C# 13. The `[Arbitrary]` source generator currently emits a separate `{TypeName}Arbitrary : IStrategyProvider<T>` class. With partial constructors, the generator can emit constructor logic directly into the user's type, enabling a more natural object-creation pattern without a separate provider class.

## .NET Advantage

C# 14's partial constructors allow source generators to emit constructor implementations directly into user-defined types. This extends Conjecture's existing compile-time code generation approach — the generated strategy logic becomes part of the type itself, visible in IDE navigation, debuggable, and requiring no separate provider class.

## Key Ideas

```csharp
// User writes:
[Arbitrary]
public partial class Person
{
    public string Name { get; init; }
    public int Age { get; init; }

    public partial Person(); // declaring declaration
}

// Generator emits:
public partial class Person
{
    public partial Person() // implementing declaration
    {
        // Populated by Conjecture during test execution
    }
}
```

- Generator emits the implementing declaration of the partial constructor
- Constructor body integrates with `IGeneratorContext` to draw values for each property/parameter
- Works alongside existing `IStrategyProvider<T>` approach — partial constructors are an alternative, not a replacement
- Supports constructor initializers (`: this(...)`, `: base(...)`) in the implementing declaration only

## Design Decisions to Make

1. How does a partial constructor interact with `IGeneratorContext`? The constructor runs outside strategy context — need an ambient context or factory pattern.
2. Should this replace or supplement the existing `{TypeName}Arbitrary` pattern?
3. How to handle types with multiple constructors? Only one can be partial.
4. Primary constructor syntax conflicts: C# 14 says "Only one partial type declaration can include the primary constructor syntax."
5. Validation: how to validate required properties are set when the constructor body is generated?

## Scope Estimate

Medium. Requires significant source generator changes and careful API design for the ambient context problem. ~2 cycles.

## Dependencies

- C# 14 compiler with partial constructor support
- Existing `Conjecture.Generators` incremental source generator
- `TypeModel` intermediate representation for property/parameter extraction

## Open Questions

- Is the ambient context pattern (e.g., `AsyncLocal<IGeneratorContext>`) acceptable for constructor injection?
- How does this interact with record types that use primary constructors?
- Should the generator warn if a user declares a partial constructor on a non-`[Arbitrary]` type?
- Performance: does the ambient context lookup add measurable overhead per object construction?
