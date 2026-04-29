# Conjecture.JsonSchema

JSON Schema-driven generation for [Conjecture](https://github.com/kommundsen/Conjecture) property-based testing. Produces `JsonElement` values that conform to a Draft 7+ JSON Schema (file, string, or parsed `JsonElement`), so you can fuzz consumers of any contract that has a published schema.

## Install

```
dotnet add package Conjecture.Core
dotnet add package Conjecture.JsonSchema
```

## Usage

```csharp
using System.Text.Json;
using Conjecture.Core;
using Conjecture.JsonSchema;

string schema = """
{
  "type": "object",
  "required": ["name", "age"],
  "properties": {
    "name": { "type": "string", "minLength": 1 },
    "age":  { "type": "integer", "minimum": 0, "maximum": 150 }
  }
}
""";

Strategy<JsonElement> people = Strategy.FromJsonSchema(schema);

// Sample values for use in property tests, fixture seeding, contract probing, etc.
JsonElement person = people.Sample();
Console.WriteLine(person.ToString());
```

`Strategy.FromJsonSchema` accepts a string, a `FileInfo`, or a parsed `JsonElement` — pick whichever is convenient.

## Types

| Type | Role |
|---|---|
| `Strategy.FromJsonSchema(string)` / `(FileInfo)` / `(JsonElement)` | Returns `Strategy<JsonElement>` conforming to the supplied schema. |
| `JsonSchemaType` | Enum of the JSON Schema primitive types (`String`, `Integer`, `Number`, `Boolean`, `Object`, `Array`, `Null`, `Any`). |

## Links

- [GitHub](https://github.com/kommundsen/Conjecture)
- [Documentation](https://ommundsen.dev/Conjecture/)
- [License](https://github.com/kommundsen/Conjecture/blob/main/LICENSE-MIT.txt)