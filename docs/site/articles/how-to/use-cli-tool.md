# How to use the CLI tool

`Conjecture.Tool` provides `conjecture generate` and `conjecture plan` commands for generating test data from your strategies at the command line — useful for seeding databases, generating fixture files, or integrating with other tooling.

## Install

```bash
dotnet tool install --global Conjecture.Tool
```

## Generate data from a strategy

```bash
conjecture generate \
  --assembly MyProject.Tests.dll \
  --type MyProject.Tests.PersonProvider \
  --count 100 \
  --format json \
  --output people.json
```

| Flag | Description |
|---|---|
| `--assembly` | Path to the assembly containing your `IStrategyProvider<T>` |
| `--type` | Fully qualified type name of the provider |
| `--count` | Number of values to generate |
| `--format` | Output format: `json` or `jsonl` |
| `--output` | Output file path (omit to write to stdout) |
| `--seed` | Optional seed for reproducible output |

## Example provider

```csharp
// In your test project:
public sealed class PersonProvider : IStrategyProvider<Person>
{
    public Strategy<Person> Create() =>
        from name in Generate.Strings(minLength: 1, maxLength: 50)
        from age in Generate.Integers<int>(18, 99)
        select new Person(name, age);
}
```

Then generate:

```bash
conjecture generate \
  --assembly bin/Debug/net10.0/MyProject.Tests.dll \
  --type MyProject.Tests.PersonProvider \
  --count 500 \
  --format json \
  --output seed-people.json
```

## Use a generation plan

For multi-step generation with references between steps, define a plan file:

```json
{
  "assembly": "bin/Debug/net10.0/MyProject.Tests.dll",
  "output": {
    "file": "seed-data.json",
    "format": "json"
  },
  "steps": [
    { "name": "customers", "type": "MyProject.Tests.CustomerProvider", "count": 100 },
    { "name": "orders", "type": "MyProject.Tests.OrderProvider", "count": 500 }
  ]
}
```

Run the plan:

```bash
conjecture plan generation-plan.json
```

The plan runner executes each step in order and writes all results to the configured output.

## Use custom types in a plan

Plan steps are not limited to built-in types like `System.Int32` or `System.String`. Any type that has an `IStrategyProvider<T>` in the plan assembly is supported.

Define the type and its provider in your test project:

```csharp
public record Location(int CityCode);

public sealed class LocationProvider : IStrategyProvider<Location>
{
    public Strategy<Location> Create() =>
        Generate.Integers<int>(1, 999).Select(static code => new Location(code));
}
```

Then reference the type by its fully qualified name in a plan step. A later step can bind to a property of the generated objects using a `$ref` with dot-path navigation:

```json
{
  "assembly": "path/to/MyAssembly.dll",
  "steps": [
    { "name": "locations", "type": "MyNamespace.Location", "count": 5, "seed": 1 },
    {
      "name": "orders",
      "type": "System.Int32",
      "count": 5,
      "seed": 2,
      "bindings": {
        "Value": { "$ref": "locations[*].CityCode" }
      }
    }
  ],
  "output": { "format": "json", "file": null }
}
```

The `[*]` in a `$ref` expands all elements from that step; the dot-path after it navigates nested JSON properties on each serialised object. So `locations[*].CityCode` collects the `CityCode` value from every generated `Location`, and the `orders` step samples from those collected values.

The provider must have a public parameterless constructor so the plan runner can instantiate it via reflection.

## Reproducible output

Fix the seed for deterministic generation:

```bash
conjecture generate --assembly ... --type ... --count 100 --seed 12345 --output data.json
```

Or in a plan:

```json
{
  "steps": [
    { "name": "customers", "type": "MyProject.Tests.CustomerProvider", "count": 100, "seed": 12345 }
  ]
}
```

## See also

- [How to use DataGen outside tests](use-data-gen.md) — programmatic generation in C#
- [Reference: Formatters](../reference/formatters.md) — JSON and JSONL output formats
