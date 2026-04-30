# Conjecture.Formatters

Built-in output formatters for [Conjecture](https://github.com/kommundsen/Conjecture) standalone data generation.

Use these formatters with `Strategy<T>.Sample` / `Strategy<T>.Stream` to serialize generated values to JSON or NDJSON — useful for seeding databases, building test fixtures, or exporting datasets.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.Formatters
```

## Usage

```csharp
using Conjecture.Core;
using Conjecture.Formatters;

// Generate 100 integers and write them as a JSON array
IOutputFormatter formatter = new JsonOutputFormatter();
IReadOnlyList<int> values = Strategy.Integer().Sample(100);
await formatter.WriteAsync(values, File.OpenWrite("data.json"));

// Or as newline-delimited JSON (NDJSON)
IOutputFormatter ndjson = new JsonLinesOutputFormatter();
await ndjson.WriteAsync(values, File.OpenWrite("data.jsonl"));
```

## Formatters

| Type | `Name` | Output |
|---|---|---|
| `JsonOutputFormatter` | `"json"` | JSON array |
| `JsonLinesOutputFormatter` | `"jsonl"` | Newline-delimited JSON (NDJSON) |

Both formatters implement `IOutputFormatter` from `Conjecture.Core`, so you can swap them or supply your own.

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
