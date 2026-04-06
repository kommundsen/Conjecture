# Source Generators

Conjecture includes a Roslyn incremental source generator (bundled in `Conjecture.Core`) that derives `IStrategyProvider<T>` implementations from your types at compile time. No additional packages are needed.

## Usage

Mark a type with `[Arbitrary]`:

```csharp
using Conjecture.Core;

[Arbitrary]
public partial record Person(string Name, int Age);
```

The generator emits a `PersonArbitrary` class:

```csharp
// Auto-generated
public sealed class PersonArbitrary : IStrategyProvider<Person>
{
    public Strategy<Person> Create() =>
        Generate.Compose<Person>(ctx =>
        {
            var name = ctx.Generate(/* resolved strategy for string */);
            var age = ctx.Generate(/* resolved strategy for int */);
            return new Person(name, age);
        });
}
```

Use it in tests:

```csharp
[Property]
public bool People_have_names([From<PersonArbitrary>] Person person)
{
    return person.Name is not null;
}
```

## Requirements

- The type must be `partial` (the generator needs to emit companion code)
- Must have at least one accessible constructor
- All constructor parameter types must have auto-resolvable strategies (primitives, strings, collections, enums, or other `[Arbitrary]` types)

## Diagnostics

The generator reports errors when requirements aren't met:

| ID | Severity | Description |
|---|---|---|
| CON200 | Error | No accessible constructor found |
| CON201 | Error | Type is not `partial` |
| CON202 | Warning | Unsupported member type (parameter type has no resolvable strategy) |

## Supported Types

| Type Category | Examples |
|---|---|
| Integer types | `int`, `long`, `byte`, `short`, `uint`, `ulong`, etc. |
| Floating point | `double`, `float` |
| Other primitives | `bool`, `string`, `char` |
| Nullable value types | `int?`, `double?`, etc. |
| Enums | Any `enum` type |
| Collections | `List<T>`, `IReadOnlyList<T>`, etc. (with supported element type) |
| Nested `[Arbitrary]` | Types that also have `[Arbitrary]` |

## Nested Types

`[Arbitrary]` types can reference each other:

```csharp
[Arbitrary]
public partial record Address(string Street, string City, int ZipCode);

[Arbitrary]
public partial record Customer(string Name, Address ShippingAddress);
```

The generator resolves `Address` via `AddressArbitrary` when generating `Customer`.

## Classes and Structs

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
