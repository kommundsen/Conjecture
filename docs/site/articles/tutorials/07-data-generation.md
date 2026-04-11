# Tutorial 7: Data Generation Outside Tests

In previous tutorials you used Conjecture strategies inside `[Property]` tests. This tutorial shows how to use the same strategies outside a test runner — to generate seed data, populate a database, or create fixture files.

## Prerequisites

- Completed [Tutorial 2](02-strategies-and-composition.md) (strategies and composition)
- A .NET 10 project (console app, script, or test utility)

## Install the packages

```bash
dotnet add package Conjecture.Core
dotnet add package Conjecture.Formatters
```

## Define a strategy

We'll generate people with names and ages:

```csharp
using Conjecture.Core;

Strategy<Person> personStrategy =
    from name in Generate.Strings(minLength: 2, maxLength: 30)
    from age in Generate.Integers<int>(18, 99)
    select new Person(name, age);

public record Person(string Name, int Age);
```

## Generate a list

`DataGen.Sample<T>` generates a `List<T>`:

```csharp
List<Person> people = DataGen.Sample(personStrategy, count: 100);

foreach (Person person in people)
{
    Console.WriteLine($"{person.Name}, age {person.Age}");
}
```

Run this and you get 100 different people with random names and ages.

## Make it reproducible

Pin the seed to get the same 100 people every time:

```csharp
List<Person> people = DataGen.Sample(personStrategy, count: 100, seed: 0xABCD1234);
```

The seed is a `ulong`. Any value works; the same value always produces the same output.

## Generate a single value

`DataGen.SampleOne<T>` returns one value:

```csharp
Person person = DataGen.SampleOne(personStrategy);
```

## Write to a JSON file

Combine with `JsonOutputFormatter` to write a JSON file:

```csharp
using Conjecture.Formatters;
using System.IO;

IEnumerable<Person> people = DataGen.Stream(personStrategy, count: 1000);

await using FileStream file = File.Create("people.json");
JsonOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

Open `people.json` and you'll see a JSON array of 1,000 people.

## Write JSONL for large datasets

For large datasets, JSONL (one JSON object per line) is more efficient to process:

```csharp
await using FileStream file = File.Create("people.jsonl");
JsonLinesOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

## Use with more complex strategies

`DataGen` works with any `Strategy<T>` — composed, recursive, or custom:

```csharp
// Nested types
[Arbitrary]
public partial record Address(string Street, string City, int ZipCode);

[Arbitrary]
public partial record Customer(string Name, Address ShippingAddress);

List<Customer> customers = DataGen.Sample(
    new CustomerArbitrary().Create(),
    count: 500);
```

## Stream values lazily

`DataGen.Stream<T>` generates values on demand — useful for large counts or processing pipelines:

```csharp
IEnumerable<Person> stream = DataGen.Stream(personStrategy, count: 10_000);

// Process without loading all 10,000 into memory
foreach (Person person in stream)
{
    // Insert into database, write to file, etc.
}
```

## Key takeaways

- `DataGen.Sample<T>` generates a list; `DataGen.SampleOne<T>` generates one value; `DataGen.Stream<T>` generates lazily
- All methods accept an optional `seed` for reproducible output
- `JsonOutputFormatter` and `JsonLinesOutputFormatter` write generated data to files
- Any `Strategy<T>` works — including composed, recursive, and `[Arbitrary]`-derived strategies

## Next

- [How to use DataGen outside tests](../how-to/use-data-gen.md) — quick reference
- [Reference: Strategies](../reference/strategies.md) — all `Generate.*` methods
- [Reference: Formatters](../reference/formatters.md) — JSON and JSONL options
