# Draft: Standalone Data Generation

## Motivation

Conjecture's strategy engine is a powerful data generation system, but it's currently only accessible inside `[Property]` test methods. Many teams need realistic, structured test data outside of tests: seeding development databases, populating staging environments, generating API fixtures, creating sample datasets for demos, and feeding integration test harnesses. Exposing the strategy engine as a standalone tool unlocks this entire use case.

## .NET Advantage

The existing `Conjecture.Mcp` dotnet tool already demonstrates that Conjecture's engine can operate outside a test runner. .NET's `dotnet tool` infrastructure provides global/local tool installation, and `System.Text.Json` serialization (with .NET 10's strict mode and PipeWriter support) enables high-throughput structured output. The `[Arbitrary]` source generator means user-defined types can be generated without reflection.

## Key Ideas

### CLI Tool
```bash
# Generate 1000 Person records as JSON
dotnet conjecture generate --type MyApp.Person --count 1000 --output persons.json

# Generate with seed for reproducibility
dotnet conjecture generate --type MyApp.Order --count 500 --seed 42 --format csv

# Generate with constraints
dotnet conjecture generate --type MyApp.Person --count 100 --settings '{"MaxExamples": 100}'

# Pipe to another tool
dotnet conjecture generate --type MyApp.Event --count 10000 | dotnet ef database seed --stdin
```

### Programmatic API
```csharp
// Outside of test context
var people = Generate.Integers<int>(1, 100)
    .Sample(count: 50, seed: 42);

var dataset = Generate.From<PersonArbitrary>()
    .SampleMany(count: 1000)
    .ToList();

// Stream large datasets
await foreach (var record in Generate.From<OrderArbitrary>().Stream(count: 100_000, seed: 42))
{
    await writer.WriteAsync(record);
}
```

### Output Formats
- JSON (default, via `System.Text.Json`)
- CSV (flat types)
- JSON Lines (streaming)
- Custom `IOutputFormatter` extensibility

### Integration Points
- EF Core database seeding
- HTTP API test fixtures
- Message queue population (Azure Service Bus, RabbitMQ)
- File-based test data (`*.json`, `*.csv` in test projects)

## Design Decisions to Make

1. Extend existing `Conjecture.Mcp` tool or create a new `Conjecture.Tool` / `conjecture-cli`?
2. How to discover user-defined types and their strategies from a compiled assembly? (Reflection vs requiring a generator project reference)
3. Should `Sample()` / `SampleMany()` be on `Strategy<T>` directly or a separate `DataGen` class?
4. Shrinking doesn't apply to data generation — should the API clearly separate generation from testing?
5. Streaming large datasets: how to handle memory for 100K+ records?
6. Deterministic output: given the same seed and type, output must be identical across runs.

## Scope Estimate

Medium. CLI scaffolding + `Sample()`/`Stream()` APIs are ~2 cycles. Output formatters and EF Core integration add ~1-2 more.

## Dependencies

- `Conjecture.Core` strategy engine
- `System.Text.Json` for serialization
- `System.CommandLine` or existing MCP infrastructure for CLI
- `[Arbitrary]` source generator for user-defined types

## Open Questions

- Should the CLI tool load user assemblies dynamically (like `dotnet ef` does) or require a project reference?
- How to handle types that require complex strategy composition (not just `[Arbitrary]`)?
- Should we support generating data that satisfies relational constraints (e.g., foreign keys across tables)?
- Is there demand for non-.NET output targets (e.g., generate data for a Python service's test suite)?
