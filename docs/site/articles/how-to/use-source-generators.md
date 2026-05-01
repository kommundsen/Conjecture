# How to use source generators

Conjecture includes a Roslyn incremental source generator (bundled in `Conjecture.Core`) that derives `IStrategyProvider<T>` implementations from your types at compile time. No additional packages needed.

## Mark a type with `[Arbitrary]`

```csharp
using Conjecture.Core;

[Arbitrary]
public partial record Person(string Name, int Age);
```

The generator emits a `PersonArbitrary` class that implements `IStrategyProvider<Person>`:

```csharp
// Auto-generated
public sealed class PersonArbitrary : IStrategyProvider<Person>
{
    public Strategy<Person> Create() =>
        Strategy.Compose<Person>(ctx =>
        {
            var name = ctx.Generate(/* resolved strategy for string */);
            var age = ctx.Generate(/* resolved strategy for int */);
            return new Person(name, age);
        });
}
```

## Use it in tests

```csharp
[Property]
public bool People_have_names([From<PersonArbitrary>] Person person)
{
    return person.Name is not null;
}
```

## Requirements

- The type must be `partial` (the generator emits companion code alongside it)
- Must have at least one accessible constructor
- All constructor parameter types must have auto-resolvable strategies: primitives, strings, collections, enums, or other `[Arbitrary]` types

## Nested types

`[Arbitrary]` types can reference each other — the generator resolves them automatically:

```csharp
[Arbitrary]
public partial record Address(string Street, string City, int ZipCode);

[Arbitrary]
public partial record Customer(string Name, Address ShippingAddress);
```

When generating `Customer`, the generator uses `AddressArbitrary` to produce the `ShippingAddress` parameter.

## Classes and structs

Works with records, classes, and structs — any type with a constructor:

```csharp
[Arbitrary]
public partial class Order
{
    public Order(int id, string product, int quantity) { /* ... */ }
}

[Arbitrary]
public partial struct Point
{
    public Point(double x, double y) { /* ... */ }
}
```

## Supported parameter types

| Category | Examples |
|---|---|
| Integer types | `int`, `long`, `byte`, `short`, `uint`, `ulong`, etc. |
| Floating point | `double`, `float` |
| Other primitives | `bool`, `string`, `char` |
| Nullable value types | `int?`, `double?`, etc. |
| Enums | Any `enum` type |
| Collections | `List<T>`, `IReadOnlyList<T>` (with supported element type) |
| Nested `[Arbitrary]` | Other types marked with `[Arbitrary]` |

## Generator diagnostics

| ID | Severity | Description |
|---|---|---|
| CON200 | Error | No accessible constructor found |
| CON201 | Error | Type is not `partial` |
| CON202 | Warning | Parameter type has no resolvable strategy |

## See also

- [How to use Strategy.For&lt;T&gt;()](use-generate-for.md) — overrides, constraint attributes, recursive types
- [Reference: Strategy.For&lt;T&gt;()](../reference/generate-for.md) — attribute table, primitive mapping, diagnostics
- [Reference: Analyzers](../reference/analyzers.md) — runtime analyzer rules (CON100–CON111, CJ0050)
- [Reference: Attributes](../reference/attributes.md) — `[Arbitrary]`, `[From<T>]` full reference
