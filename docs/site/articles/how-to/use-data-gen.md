# How to use DataGen outside tests

`DataGen` is a standalone data generation API that runs Conjecture strategies without a test runner. Use it to generate seed data, benchmark inputs, CLI fixtures, or any scenario where you need random-but-reproducible values outside a `[Property]` test.

## Generate a list of values

```csharp
using Conjecture.Core;

// 100 random integers
List<int> ints = DataGen.Sample(Strategy.Integers<int>(1, 1000), count: 100);

// 50 random strings
List<string> names = DataGen.Sample(Strategy.Strings(minLength: 3, maxLength: 20), count: 50);
```

## Generate a single value

```csharp
int value = DataGen.SampleOne(Strategy.Integers<int>(0, int.MaxValue));
string name = DataGen.SampleOne(Strategy.Strings(minLength: 1, maxLength: 50));
```

## Pin the seed for reproducibility

Every method accepts an optional `seed` parameter:

```csharp
// Always produces the same 100 integers
List<int> reproducible = DataGen.Sample(
    Strategy.Integers<int>(1, 1000),
    count: 100,
    seed: 0xDEADBEEF);
```

## Stream values lazily

`DataGen.Stream<T>` returns an `IEnumerable<T>` that generates values on demand:

```csharp
IEnumerable<string> emails = DataGen.Stream(
    Strategy.Strings(5, 30),
    count: 10_000);

foreach (string email in emails)
{
    // Process each value without loading all 10,000 into memory
}
```

## Use with custom strategies

`DataGen` works with any `Strategy<T>`, including composed and custom ones:

```csharp
Strategy<Person> personStrategy =
    from name in Strategy.Strings(minLength: 1, maxLength: 50)
    from age in Strategy.Integers<int>(18, 99)
    select new Person(name, age);

List<Person> people = DataGen.Sample(personStrategy, count: 500);
```

## Export to JSON

Combine with `JsonOutputFormatter` from `Conjecture.Formatters` to write generated data to a file:

```csharp
using Conjecture.Core;
using Conjecture.Formatters;
using System.IO;

IEnumerable<Person> people = DataGen.Stream(personStrategy, count: 1000);

await using FileStream file = File.Create("seed-data.json");
JsonOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

For newline-delimited JSON (JSONL):

```csharp
JsonLinesOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

## See also

- [Tutorial 7: Data Generation](../tutorials/07-data-generation.md) — walkthrough with a complete example
- [Reference: Formatters](../reference/formatters.md) — `JsonOutputFormatter`, `JsonLinesOutputFormatter`
- [Reference: Strategies](../reference/strategies.md) — all `Strategy.*` factory methods
