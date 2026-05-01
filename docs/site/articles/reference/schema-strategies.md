# Schema strategies reference

Reference for `Conjecture.JsonSchema`, `Conjecture.OpenApi`, and `Conjecture.Protobuf` APIs.

## JSON Schema constraint mapping

How JSON Schema keywords map to Conjecture generation behavior.

| JSON Schema keyword | Generated value |
|---|---|
| `"type": "boolean"` | `true` or `false` |
| `"type": "integer"` | `long` in `[minimum, maximum]`; defaults `[0, 1000]` |
| `"type": "number"` | `double` in `[minimum, maximum]`; defaults `[0.0, 1000.0]` |
| `"type": "string"` | String of length `[minLength, maxLength]`; defaults `[0, 20]` |
| `"type": "array"` | Array of `[minItems, maxItems]` items; defaults `[0, 10]` |
| `"type": "object"` | Object with declared `properties`; optional properties included ~50% of the time |
| `"enum": [...]` | One element sampled uniformly from the array |
| `"const": value` | Always produces `value` |
| `"oneOf": [...]` | One sub-schema chosen uniformly |
| `"anyOf": [...]` | One sub-schema chosen uniformly |
| `"allOf": [...]` | Properties merged across all sub-schemas; type forced to `object` |
| `"$ref": "#/$defs/X"` | Resolved to the named definition; cyclic refs limited by `maxDepth` |
| `"minimum"` / `"maximum"` | Inclusive bounds for integer and number |
| `"exclusiveMinimum": true` | Increments the lower bound by 1 (integer) |
| `"exclusiveMaximum": true` | Decrements the upper bound by 1 (integer) |
| `"minLength"` / `"maxLength"` | Inclusive string length bounds |
| `"pattern"` | String matching the regex via `Strategy.Matching` |
| `"minItems"` / `"maxItems"` | Inclusive array length bounds |
| `"required"` | Listed properties always appear in generated objects |
| `"format": "email"` | Email address via `Strategy.Email()` |
| `"format": "uri"` | URI via `Strategy.Url()` |
| `"format": "uuid"` | UUID via `Strategy.Uuid()` |
| `"format": "date-time"` | ISO 8601 date-time string via `Strategy.IsoDate()` |
| `"format": "ipv4"` | IPv4 address string |
| `"format": "ipv6"` | IPv6 address string |
| `"format": "date"` | ISO 8601 date string |
| `"format": "time"` | ISO 8601 time string |
| Other `"format"` values | Falls back to plain string generation |

### Deferred keywords

The following keywords are parsed but do not constrain generation in the current implementation:

`additionalProperties`, `patternProperties`, `unevaluatedProperties`, `if`/`then`/`else`,
`not`, `contains`, `minContains`, `maxContains`, `uniqueItems`, `dependentSchemas`,
`dependentRequired`, `propertyNames`, `multipleOf`.

## `Strategy.FromJsonSchema` overloads

**Package:** `Conjecture.JsonSchema`

```csharp
// From a JSON string
Strategy<JsonElement> Strategy.FromJsonSchema(string jsonSchemaText)

// From a parsed JsonElement
Strategy<JsonElement> Strategy.FromJsonSchema(JsonElement root)

// From a file
Strategy<JsonElement> Strategy.FromJsonSchema(FileInfo schemaFile)
```

Throws `JsonException` (or a derived type) at construction time if the input is not valid JSON.

## `OpenApiDocument` API

**Package:** `Conjecture.OpenApi`

### `Strategy.FromOpenApi` overloads

```csharp
Task<OpenApiDocument> Strategy.FromOpenApi(string filePath)
Task<OpenApiDocument> Strategy.FromOpenApi(FileInfo file)
Task<OpenApiDocument> Strategy.FromOpenApi(Uri url)
```

Loads and parses an OpenAPI 3.x document. Accepts JSON or YAML. For `Uri`, file URIs and HTTP/HTTPS are both supported.

### `OpenApiDocument.RequestBody`

```csharp
Strategy<JsonElement> RequestBody(string method, string path, int maxDepth = 5)
```

Returns a strategy for the request body of the given operation. `method` is case-insensitive (`"GET"`, `"post"`, etc.). `path` must match the path template exactly as declared in the spec (e.g., `"/api/orders/{id}"`).

Throws `InvalidOperationException` if the operation has no request body schema.

### `OpenApiDocument.ResponseBody`

```csharp
Strategy<JsonElement> ResponseBody(string method, string path, int statusCode, int maxDepth = 5)
```

Returns a strategy for the response body of the given operation and status code. If the exact status code is not found, falls back to the first declared response.

### `OpenApiDocument.PathParameter`

```csharp
Strategy<JsonElement> PathParameter(string method, string path, string name, int maxDepth = 5)
```

Returns a strategy for a named path parameter (e.g., `"id"` in `/api/orders/{id}`).

### `OpenApiDocument.QueryParameter`

```csharp
Strategy<JsonElement> QueryParameter(string method, string path, string name, int maxDepth = 5)
```

Returns a strategy for a named query parameter.

## `Strategy.FromProtobuf` overloads

**Package:** `Conjecture.Protobuf`

```csharp
// From a compiled message type
Strategy<JsonElement> Strategy.FromProtobuf<T>(int maxDepth = 5)
    where T : IMessage<T>, new()

// From a runtime descriptor
Strategy<JsonElement> Strategy.FromProtobuf(MessageDescriptor descriptor, int maxDepth = 5)
```

`maxDepth` controls recursion depth for self-referential message types. Recursive fields beyond the limit produce a null JSON value.

### Protobuf type mapping

| Protobuf field type | Generated JSON kind |
|---|---|
| `bool` | `true` / `false` |
| `int32`, `sint32`, `fixed32`, `sfixed32` | Number (integer) |
| `int64`, `sint64`, `fixed64`, `sfixed64`, `uint32`, `uint64` | Number (integer) |
| `float` | Number (floating-point) |
| `double` | Number (floating-point) |
| `string`, `bytes` | String |
| `message` | Object (recursively generated) |
| `enum` | Number (one of the declared enum values) |
| Repeated field | Array |
| `oneof` | Object with exactly one arm present |
