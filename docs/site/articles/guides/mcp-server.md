# MCP Server

`Conjecture.Mcp` is a [Model Context Protocol](https://modelcontextprotocol.io) server that exposes Conjecture's API to AI assistants (Claude, Copilot, Cursor, etc.). It provides three tools and a set of API reference resources.

## Setup

```bash
dotnet tool install --global Conjecture.Mcp
```

Add it to your `.mcp.json`:

```json
{
  "mcpServers": {
    "conjecture": {
      "type": "stdio",
      "command": "conjecture-mcp"
    }
  }
}
```

## Tools

### `suggest-strategy`

Recommends the right `Generate.*` factory for a given C# type name.

| Input | Suggestion |
|---|---|
| `int` | `Generate.Integers<int>()` |
| `string` | `Generate.Strings()` |
| `List<bool>` | `Generate.Lists(Generate.Booleans())` |
| `IReadOnlySet<int>` | `Generate.Sets(Generate.Integers<int>())` |
| `IReadOnlyDictionary<string, int>` | `Generate.Dictionaries(Generate.Strings(), Generate.Integers<int>())` |
| `int?` | `Generate.Nullable(Generate.Integers<int>())` |
| `(int, string)` | `Generate.Tuples(Generate.Integers<int>(), Generate.Strings())` |
| `MyRecord` | Options: `Generate.Compose`, `[Arbitrary]`, `Generate.Enums`, `Generate.SampledFrom`, `.Select` |

### `scaffold-property-test`

Generates a `[Property]` test skeleton from a C# method signature. Supports `xunit` (default), `nunit`, and `mstest`.

Given `public static int Add(int a, int b)`:

```csharp
using Conjecture.Core;
using Conjecture.Xunit;
using Xunit;

public class AddPropertyTests
{
    [Property]
    public void Add_SatisfiesProperty(int a, int b)
    {
        // Act
        // var result = Add(a, b);

        // Assert
        // Assert.True(result >= 0);
    }
}
```

### `explain-shrink-output`

Parses a Conjecture test failure and returns a structured explanation:

- How many examples were tried and how many shrink steps occurred
- The minimal counterexample with each parameter labelled
- The original unshrunk input for comparison
- A `[Property(Seed = 0x...)]` snippet to reproduce the failure deterministically

## API Reference Resources

Eight read-only resources are available at `conjecture://api/*`:

| URI | Contents |
|---|---|
| `conjecture://api/strategies` | All `Generate.*` factory methods and LINQ combinators |
| `conjecture://api/settings` | `ConjectureSettings`, `ConjectureSettingsAttribute`, and configuration options |
| `conjecture://api/state-machines` | `IStateMachine<TState,TCommand>`, `StateMachineRun`, and `Generate.StateMachine` |
| `conjecture://api/shrinking` | How Conjecture finds minimal counterexamples |
| `conjecture://api/assumptions` | `Assume.That()`, `IGeneratorContext.Assume()`, and `UnsatisfiedAssumptionException` |
| `conjecture://api/attributes` | `[Property]`, `[Example]`, `[From<T>]`, `[FromFactory]`, `[ConjectureSettings]` |
| `conjecture://api/targeted-testing` | `Target.Maximize`, `Target.Minimize`, `IGeneratorContext.Target`, and targeting settings |
| `conjecture://api/recursive-strategies` | `Generate.Recursive<T>` for tree-shaped and self-referential types |
