# Conjecture.Mcp

A [Model Context Protocol](https://modelcontextprotocol.io) server that exposes Conjecture's API to AI assistants (Claude, Copilot, Cursor, etc.).

## Installation

```bash
dotnet tool install --global Conjecture.Mcp
```

Then add it to your `.mcp.json`:

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

| Tool | Description |
|---|---|
| `suggest-strategy` | Recommends the right `Generate.*` strategy for a given C# type |
| `scaffold-property-test` | Generates a `[Property]` test skeleton from a method signature |
| `explain-shrink-output` | Parses and explains a Conjecture test failure |

## API Reference Resources

Eight read-only reference resources are exposed at `conjecture://api/*`: `strategies`, `settings`, `state-machines`, `shrinking`, `assumptions`, `attributes`, `targeted-testing`, `recursive-strategies`.
