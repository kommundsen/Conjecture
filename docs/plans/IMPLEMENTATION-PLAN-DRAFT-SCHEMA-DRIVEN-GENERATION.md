# Draft: Schema-Driven Generation

## Motivation

APIs are defined by schemas — OpenAPI (Swagger), JSON Schema, Protobuf, GraphQL, Avro. These schemas precisely describe the shape, types, and constraints of data flowing through a system. Rather than manually writing strategies for every API type, Conjecture can read a schema and automatically produce strategies that generate valid (and intentionally boundary-pushing) data conforming to that schema. This is the fastest path from "I have an API" to "I'm property-testing it."

## .NET Advantage

.NET has mature schema tooling:
- `Microsoft.OpenApi` parses OpenAPI/Swagger specs
- `System.Text.Json` with .NET 10's strict serialization validates generated payloads
- `Grpc.Tools` and `Google.Protobuf` provide Protobuf reflection APIs
- NSwag/Kiota generate C# clients from OpenAPI — Conjecture can generate the *inputs* to those clients
- `System.ComponentModel.DataAnnotations` attributes (`[Range]`, `[StringLength]`, `[Required]`) are already used by Conjecture's `[Arbitrary]` source generator — schema constraints map directly

## Key Ideas

### OpenAPI / Swagger
```csharp
// Load schema and get strategies for all types
var schema = await SchemaStrategy.FromOpenApi("swagger.json");

// Strategy for a specific endpoint's request body
Strategy<JsonElement> createOrderRequest = schema.RequestBody("POST", "/api/orders");

// Strategy for a response type
Strategy<JsonElement> orderResponse = schema.ResponseBody("GET", "/api/orders/{id}", statusCode: 200);

// Use in a property test
[Property]
public async Task<bool> CreateOrderRoundTrips(
    [FromSchema("swagger.json", "POST", "/api/orders")] JsonElement request)
{
    var response = await client.PostAsJsonAsync("/api/orders", request);
    return response.IsSuccessStatusCode;
}
```

### JSON Schema
```csharp
// Direct JSON Schema support
Strategy<JsonElement> strategy = SchemaStrategy.FromJsonSchema("""
{
    "type": "object",
    "properties": {
        "name": { "type": "string", "minLength": 1, "maxLength": 100 },
        "age": { "type": "integer", "minimum": 0, "maximum": 150 },
        "email": { "type": "string", "format": "email" }
    },
    "required": ["name", "age"]
}
""");
```

### Protobuf
```csharp
// Generate from a compiled Protobuf message descriptor
Strategy<CreateOrderRequest> strategy = SchemaStrategy.FromProtobuf<CreateOrderRequest>();
// Uses Protobuf reflection to discover fields, types, and constraints
```

### Schema Constraint Mapping
| Schema Constraint | Conjecture Strategy |
|---|---|
| `"type": "integer", "minimum": 0, "maximum": 100` | `Generate.Integers<int>(0, 100)` |
| `"type": "string", "minLength": 1, "maxLength": 50` | `Generate.Strings(minLength: 1, maxLength: 50)` |
| `"type": "string", "format": "email"` | `Generate.Emails()` (new format-aware strategy) |
| `"type": "string", "format": "date-time"` | `Generate.DateTimes().Select(d => d.ToString("O"))` |
| `"type": "string", "pattern": "^[A-Z]{3}$"` | `Generate.FromRegex("^[A-Z]{3}$")` (new) |
| `"enum": ["a", "b", "c"]` | `Generate.SampledFrom("a", "b", "c")` |
| `"oneOf": [...]` | `Generate.OneOf(...)` |
| `"type": "array", "minItems": 1` | `Generate.Lists(..., minSize: 1)` |
| `"$ref": "#/..."` | Recursive strategy resolution |

### Boundary-Pushing Generation
Beyond valid data, optionally generate *near-boundary* inputs:
- Strings at exactly `minLength` and `maxLength`
- Integers at `minimum` and `maximum`
- Arrays at `minItems` and `maxItems`
- Missing optional fields
- Extra fields (if `additionalProperties` is not `false`)

### Format-Specific Strategies
New strategies needed for common schema formats:
- `Generate.Emails()` — valid email addresses
- `Generate.Uris()` — valid URIs
- `Generate.Ipv4()` / `Generate.Ipv6()` — IP addresses
- `Generate.Uuids()` — UUID/GUID strings
- `Generate.FromRegex(pattern)` — regex-driven string generation
- `Generate.DateTimeStrings(format)` — formatted date strings

## Design Decisions to Make

1. Ship as `Conjecture.Schema` package or split per format (`Conjecture.OpenApi`, `Conjecture.Protobuf`)?
2. Generate `JsonElement` (generic) or strongly-typed C# objects (requires codegen)?
3. How to handle `$ref` cycles in JSON Schema? (Use `Generate.Recursive` with depth limit)
4. Should schema loading happen at compile time (source generator) or runtime (reflection)?
5. `Generate.FromRegex()` — implement from scratch or use a library? (Regex-to-strategy is non-trivial)
6. How to handle schema evolution? (Test that v2 schema accepts all v1-generated data)

## Scope Estimate

Large. OpenAPI + JSON Schema core is ~3 cycles. Protobuf, GraphQL, regex generation, and format strategies add ~3 more. Could be phased.

## Dependencies

- `Microsoft.OpenApi.Readers` for OpenAPI parsing
- `NJsonSchema` or manual JSON Schema parser
- `Google.Protobuf` for Protobuf reflection
- `Conjecture.Core` strategy engine
- New format-specific strategies (emails, URIs, etc.)

## Open Questions

- Should strategies from schemas be shrinkable? (Shrinking may produce values that violate schema constraints — need constrained shrinking)
- How to handle `additionalProperties`, `patternProperties`, and other complex JSON Schema features?
- Should we generate *invalid* data on purpose for negative testing? (e.g., missing required fields, wrong types)
- Is GraphQL schema support worth the complexity? (Smaller .NET user base than OpenAPI/Protobuf)
- How to handle authentication/authorization schemas? (OAuth scopes, API keys — generate valid tokens?)
- Should schema-derived strategies integrate with the MCP tool for AI-assisted test scaffolding?
