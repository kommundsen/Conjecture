# How to generate data from a JSON Schema definition

Use `Generate.FromJsonSchema` to build a `Strategy<JsonElement>` from an inline schema, a file, or a parsed `JsonElement`.

## Prerequisites

Install `Conjecture.JsonSchema`:

```bash
dotnet add package Conjecture.JsonSchema
```

## Steps

### 1. Define or load your schema

You can pass a JSON string, a `FileInfo`, or a `JsonElement`:

```csharp
using System.Text.Json;
using Conjecture.Core;
using Conjecture.JsonSchema;

// Inline string
Strategy<JsonElement> strategy = Generate.FromJsonSchema("""
    {
      "type": "object",
      "properties": {
        "name":  { "type": "string", "minLength": 1, "maxLength": 100 },
        "age":   { "type": "integer", "minimum": 0, "maximum": 150 },
        "email": { "type": "string", "format": "email" }
      },
      "required": ["name", "age"]
    }
    """);

// From a file
Strategy<JsonElement> fromFile = Generate.FromJsonSchema(new FileInfo("person.schema.json"));
```

### 2. Use the strategy in a property test

# [xUnit v2](#tab/xunit-v2)

```csharp
using Conjecture.Core;
using Conjecture.JsonSchema;
using Conjecture.Xunit;
using System.Text.Json;
using Xunit;

public class PersonSchemaTests
{
    private static readonly Strategy<JsonElement> PersonStrategy = Generate.FromJsonSchema("""
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "minLength": 1 },
            "age":  { "type": "integer", "minimum": 0, "maximum": 150 }
          },
          "required": ["name", "age"]
        }
        """);

    [Property]
    public void Person_AlwaysHasNameAndAge(JsonElement person)
    {
        Assert.True(person.TryGetProperty("name", out JsonElement name));
        Assert.Equal(JsonValueKind.String, name.ValueKind);
        Assert.True(person.TryGetProperty("age", out JsonElement age));
        Assert.Equal(JsonValueKind.Number, age.ValueKind);
    }
}
```

# [xUnit v3](#tab/xunit-v3)

```csharp
using Conjecture.Core;
using Conjecture.JsonSchema;
using Conjecture.Xunit.V3;
using System.Text.Json;
using Xunit;

public class PersonSchemaTests
{
    [Property]
    public void Person_AlwaysHasNameAndAge()
    {
        Strategy<JsonElement> strategy = Generate.FromJsonSchema("""
            { "type": "object", "properties": { "name": { "type": "string" }, "age": { "type": "integer" } }, "required": ["name", "age"] }
            """);
        IReadOnlyList<JsonElement> samples = DataGen.Sample(strategy, 50);
        foreach (JsonElement person in samples)
        {
            Assert.True(person.TryGetProperty("name", out _));
            Assert.True(person.TryGetProperty("age", out _));
        }
    }
}
```

# [NUnit](#tab/nunit)

```csharp
using Conjecture.Core;
using Conjecture.JsonSchema;
using Conjecture.NUnit;
using NUnit.Framework;
using System.Text.Json;

[TestFixture]
public class PersonSchemaTests
{
    [Property]
    public void Person_AlwaysHasNameAndAge()
    {
        Strategy<JsonElement> strategy = Generate.FromJsonSchema("""
            { "type": "object", "properties": { "name": { "type": "string" }, "age": { "type": "integer" } }, "required": ["name", "age"] }
            """);
        foreach (JsonElement person in DataGen.Sample(strategy, 50))
        {
            Assert.That(person.TryGetProperty("name", out _), Is.True);
            Assert.That(person.TryGetProperty("age", out _), Is.True);
        }
    }
}
```

# [MSTest](#tab/mstest)

```csharp
using Conjecture.Core;
using Conjecture.JsonSchema;
using Conjecture.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

[TestClass]
public class PersonSchemaTests
{
    [Property]
    public void Person_AlwaysHasNameAndAge()
    {
        Strategy<JsonElement> strategy = Generate.FromJsonSchema("""
            { "type": "object", "properties": { "name": { "type": "string" }, "age": { "type": "integer" } }, "required": ["name", "age"] }
            """);
        foreach (JsonElement person in DataGen.Sample(strategy, 50))
        {
            Assert.IsTrue(person.TryGetProperty("name", out _));
            Assert.IsTrue(person.TryGetProperty("age", out _));
        }
    }
}
```

***

## Using `$ref` and `$defs`

Schemas with `$ref` and `$defs` are resolved automatically:

```csharp
Strategy<JsonElement> strategy = Generate.FromJsonSchema("""
    {
      "$defs": {
        "Address": {
          "type": "object",
          "properties": {
            "street": { "type": "string" },
            "city":   { "type": "string" }
          },
          "required": ["street", "city"]
        }
      },
      "type": "object",
      "properties": {
        "name":    { "type": "string" },
        "address": { "$ref": "#/$defs/Address" }
      }
    }
    """);
```

Circular `$ref` chains are handled with a depth limit (default: 5). See [Schema strategies reference](../reference/schema-strategies.md) for supported keywords.

> [!NOTE]
> `Generate.FromJsonSchema` throws `JsonException` at construction time if the input is not valid JSON.
