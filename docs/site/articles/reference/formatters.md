# Formatters reference

Formatters write generated data to a stream. Used with `DataGen.Stream<T>()` to export generated values to files.

Install the formatters package:

```bash
dotnet add package Conjecture.Formatters
```

## `IOutputFormatter`

```csharp
public interface IOutputFormatter
{
    string Name { get; }
    Task WriteAsync<T>(IEnumerable<T> data, Stream output, CancellationToken ct);
}
```

| Member | Description |
|---|---|
| `Name` | Formatter identifier used by `conjecture generate --format`. |
| `WriteAsync<T>` | Writes `data` to `output` in the formatter's format. |

## `JsonOutputFormatter`

Writes values as a JSON array. Each call produces one complete JSON document.

```
Name: "json"
```

**Output format:**

```json
[
  {"name": "Alice", "age": 42},
  {"name": "Bob", "age": 23}
]
```

**Usage:**

```csharp
using Conjecture.Core;
using Conjecture.Formatters;

IEnumerable<Person> people = DataGen.Stream(personStrategy, count: 1000);

await using FileStream file = File.Create("people.json");
JsonOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

Serialization uses `System.Text.Json` with default options. To customize options, supply them via the constructor:

```csharp
JsonSerializerOptions options = new() { WriteIndented = true };
JsonOutputFormatter formatter = new(options);
```

## `JsonLinesOutputFormatter`

Writes values as newline-delimited JSON (JSONL). Each value is one line.

```
Name: "jsonl"
```

**Output format:**

```text
{"name": "Alice", "age": 42}
{"name": "Bob", "age": 23}
```

**Usage:**

```csharp
JsonLinesOutputFormatter formatter = new();
await formatter.WriteAsync(people, file, CancellationToken.None);
```

JSONL is preferable for large datasets: it streams one object at a time, allows line-by-line processing, and remains valid when truncated.

## Choosing a format

| Format | Use when |
|---|---|
| `json` | Output consumed by tools expecting a JSON array; small to medium datasets |
| `jsonl` | Large datasets; streaming processing; log-style pipelines |

## See also

- [How to use DataGen outside tests](../how-to/use-data-gen.md)
- [How to use the CLI tool](../how-to/use-cli-tool.md)
