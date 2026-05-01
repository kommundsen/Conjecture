# How to generate sealed class hierarchies

Annotate an abstract base class and each concrete subtype with `[Arbitrary]` to get a `Strategy.OneOf` strategy that picks uniformly among all subtypes.

## Requirements

- The abstract base must be `abstract`, `partial`, and annotated with `[Arbitrary]`
- Each subtype you want included must be `partial` and annotated with `[Arbitrary]`
- All types must be in the same compilation (external assembly subtypes are not detected)

## Steps

### 1. Mark the abstract base

```csharp
using Conjecture.Core;

[Arbitrary]
public abstract partial class Shape { }
```

### 2. Mark each concrete subtype

```csharp
[Arbitrary]
public partial class Circle : Shape
{
    public Circle(double radius) { }
}

[Arbitrary]
public partial class Rectangle : Shape
{
    public Rectangle(double width, double height) { }
}
```

The generator emits `ShapeArbitrary` that picks uniformly across all decorated subtypes:

```csharp
// Auto-generated
public sealed class ShapeArbitrary : IStrategyProvider<Shape>
{
    public Strategy<Shape> Create() =>
        Strategy.OneOf(
            new CircleArbitrary().Create().Select(static x => (Shape)x),
            new RectangleArbitrary().Create().Select(static x => (Shape)x)
        );
}
```

### 3. Use it in a property test

# [xUnit v2](#tab/xunit-v2)

```csharp
[Property]
public bool Shape_area_is_non_negative([From<ShapeArbitrary>] Shape shape)
{
    return shape.Area() >= 0;
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
[Property]
public bool Shape_area_is_non_negative([From<ShapeArbitrary>] Shape shape)
{
    return shape.Area() >= 0;
}
```

# [NUnit](#tab/nunit)

```csharp
[Property]
public bool Shape_area_is_non_negative([From<ShapeArbitrary>] Shape shape)
{
    return shape.Area() >= 0;
}
```

# [MSTest](#tab/mstest)

```csharp
[Property]
public bool Shape_area_is_non_negative([From<ShapeArbitrary>] Shape shape)
{
    return shape.Area() >= 0;
}
```

***

## Undecorated subtypes

If a concrete subtype exists but is not annotated with `[Arbitrary]`, Conjecture emits a **CON205** warning at the subtype declaration site:

```csharp
[Arbitrary]
public abstract partial class Shape { }

public partial class Triangle : Shape { }  // CON205: excluded from ShapeArbitrary
```

`Triangle` will not appear in the generated `OneOf`. Add `[Arbitrary]` to include it, or suppress CON205 if exclusion is intentional.

## See also

- [Understanding sealed hierarchy strategies](../explanation/sealed-hierarchy-strategies.md) — design rationale and scope constraints
- [Reference: Analyzers](analyzers.md) — CON205, CON300–CON302 diagnostic details
- [How to use source generators](use-source-generators.md) — generating strategies for concrete types
