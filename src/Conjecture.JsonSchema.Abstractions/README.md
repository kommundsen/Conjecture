# Conjecture.JsonSchema.Abstractions

Schema-format adapter contracts for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Reference this package when building a new schema-format adapter (e.g. YAML Schema, AsyncAPI). End-user test code should reference [`Conjecture.JsonSchema`](https://www.nuget.org/packages/Conjecture.JsonSchema) instead.

## Who is this for?

Authors building a strategy provider driven by a schema format that compiles down to the `JsonSchemaNode` tree — for example, a YAML Schema reader or an AsyncAPI message schema adapter.

## Install

```
dotnet add package Conjecture.JsonSchema.Abstractions
```

## Types

| Type | Role |
|---|---|
| `JsonSchemaNode` | Immutable record representing a parsed JSON Schema node. Build or transform this tree, then pass it to `Strategy.FromJsonSchema(node)` from `Conjecture.JsonSchema`. |
| `JsonSchemaType` | Enum of JSON Schema primitive types (`Boolean`, `Integer`, `Number`, `String`, `Array`, `Object`, etc.). |
| `JsonSchemaParser` | `Parse(JsonElement root)` — converts a JSON Schema document element into a `JsonSchemaNode` tree. |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)
