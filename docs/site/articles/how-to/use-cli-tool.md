# How to use the CLI tool

`Conjecture.Tool` provides a `conjecture generate` command for generating test data from your strategies at the command line — useful for seeding databases, generating fixture files, or integrating with other tooling.

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

```yaml
# generation-plan.yaml
assembly: bin/Debug/net10.0/MyProject.Tests.dll
output:
  file: seed-data.json
  format: json
steps:
  - name: customers
    type: MyProject.Tests.CustomerProvider
    count: 100
  - name: orders
    type: MyProject.Tests.OrderProvider
    count: 500
```

Run the plan:

```bash
conjecture generate --plan generation-plan.yaml
```

The plan runner executes each step in order and writes all results to the configured output.

## Reproducible output

Fix the seed for deterministic generation:

```bash
conjecture generate --assembly ... --type ... --count 100 --seed 12345 --output data.json
```

Or in a plan:

```yaml
steps:
  - name: customers
    type: MyProject.Tests.CustomerProvider
    count: 100
    seed: 12345
```

## See also

- [How to use DataGen outside tests](use-data-gen.md) — programmatic generation in C#
- [Reference: Formatters](../reference/formatters.md) — JSON and JSONL output formats
