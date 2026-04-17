# 0051. Sealed Class Hierarchy Strategy Design

**Date:** 2026-04-17
**Status:** Accepted

## Context

Conjecture's source generator currently handles `[Arbitrary]`-decorated types by generating
`IStrategyProvider<T>` implementations that call constructors or initializers. Abstract base
types (abstract classes and abstract records) cannot be instantiated directly, so the existing
path does not apply. Users who want to generate values for a sealed class hierarchy need a way
to tell Conjecture how to compose the concrete subtypes into a single strategy.

The goal is to allow this pattern:

```csharp
[Arbitrary] public abstract class Shape { }
[Arbitrary] public sealed class Circle(double Radius) : Shape { }
[Arbitrary] public sealed class Rectangle(double W, double H) : Shape { }
```

and have Conjecture emit a `ShapeArbitrary` that returns `Generate.OneOf(...)` across all
decorated concrete subtypes, without any hand-written glue code.

## Decision

- **Detection:** When `[Arbitrary]` is placed on an `abstract` class or `abstract` record,
  the generator routes to a new hierarchy-extraction path rather than the existing
  constructor/initializer path. Interfaces are **not** supported.

- **Subtype discovery:** The generator scans the current compilation only. A concrete
  (non-abstract) type qualifies as a subtype if it (a) carries `[Arbitrary]` and (b) has the
  abstract base in its direct or indirect inheritance chain. Cross-assembly subtype discovery
  is out of scope for v1.

- **Non-decorated concrete subtypes:** Concrete types that inherit from the abstract base but
  lack `[Arbitrary]` trigger a CON205 diagnostic (warning, not error) at the declaration site
  of the concrete subtype. They are excluded from the generated `OneOf` call.

- **Emission:** The generator produces:

  ```csharp
  public sealed class BaseTypeArbitrary : IStrategyProvider<BaseType>
  {
      public Strategy<BaseType> GetStrategy() =>
          Generate.OneOf(
              new SubtypeAArbitrary().GetStrategy().Select(s => (BaseType)s),
              new SubtypeBArbitrary().GetStrategy().Select(s => (BaseType)s));
  }
  ```

- **Weighting:** Equal weighting only. User-configurable weights are deferred to a future
  release.

- **Supported base kinds:** Abstract classes and abstract records. Interfaces are excluded
  because C# `record` cannot implement interfaces in the same struct-like pattern and the
  semantics are sufficiently different to warrant a separate design.

## Consequences

- Users get zero-boilerplate hierarchy generation by decorating every relevant type with
  `[Arbitrary]`.
- Forgetting to decorate a concrete subtype produces a visible CON205 warning rather than a
  silent omission, which is the safer failure mode.
- Equal weighting means rarer subtypes may be under-tested in large hierarchies; this is a
  known limitation documented for v1.
- Cross-assembly hierarchies (e.g., library base type, test-project subtypes) are not
  supported and must be composed manually.

## Alternatives Considered

- **Opt-in attribute on each subtype only (no base decoration):** Rejected because it
  provides no single place to assert that the whole hierarchy is covered, and the generator
  has no way to emit the base-type provider.

- **Interface support:** Deferred. Interfaces introduce ambiguity (a type can implement many
  interfaces) that requires a separate attribution strategy.

- **User-configurable weights via attribute parameters:** Deferred to v2. Adds API surface
  and complexity before the basic scenario is validated.

- **Cross-assembly discovery via MSBuild item groups:** Too complex for v1; would require
  changes to the build integration layer.
