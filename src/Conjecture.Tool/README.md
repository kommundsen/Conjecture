# Conjecture.Tool

CLI for [Conjecture](https://github.com/kommundsen/Conjecture) standalone data generation. Loads an assembly that exposes `IStrategyProvider<T>` types, samples them, and writes the result to stdout or a file as JSON or NDJSON. Useful for seeding test fixtures, building deterministic datasets, or pre-computing replay corpora outside a test runner.

## Install

```bash
dotnet tool install --global Conjecture.Tool
```

## Usage

```bash
# Generate 100 JSON examples of a type whose strategy provider lives in MyAssembly.dll
conjecture generate \
  --assembly ./bin/Debug/net10.0/MyAssembly.dll \
  --type MyNamespace.MyClass \
  --count 100 \
  --seed 12345 \
  --format json \
  --output examples.json

# Run a multi-step generation plan from a JSON file
conjecture plan ./fixtures.plan.json
```

A plan file (`fixtures.plan.json`) describes a sequence of generation steps and lets later steps reference earlier ones via `$ref` expressions; output is buffered through `Conjecture.Formatters` (`json` or `jsonl`).

## Commands

| Command | Purpose |
|---|---|
| `conjecture generate --assembly <path> --type <name> [--count N] [--seed S] [--format json\|jsonl] [--output <file>]` | Sample N values of `<name>` from a provider in `<assembly>` and write them to stdout or `--output`. |
| `conjecture plan <plan.json>` | Run a multi-step `GenerationPlan` (steps, bindings via `$ref`, and a top-level `Output` config). |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
