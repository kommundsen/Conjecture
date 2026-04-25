# 0060. Schema-Driven Generation Package Layout and Design Choices

**Date:** 2026-04-25
**Status:** Accepted

## Context

APIs are described by schemas — OpenAPI/Swagger, JSON Schema, Protobuf — that already specify the shape, types, and constraints of every payload that crosses the boundary. Conjecture should be able to read those schemas and produce strategies that generate valid (and intentionally boundary-pushing) data conforming to them, so users do not have to hand-author a strategy for every DTO.

This ADR records the package layout and the cross-cutting design choices that constrain implementation cycles 73.1–73.10. Constraints driving the design:

- **Reflection / AOT**: Conjecture's satellite packages must stay trim/AOT-friendly where possible. A single umbrella package would pull every parser into every consumer; split packages let users opt in to only what they need.
- **Shrinking fidelity**: Conjecture shrinks by manipulating the choice-sequence IR, not the produced value. A schema strategy that emits opaque JSON would never shrink toward minimal counter-examples.
- **`$ref` cycles**: real-world schemas (OpenAPI, JSON Schema) routinely contain self-referential definitions (`Tree` referencing `Tree`). Naive resolution recurses forever.
- **Format diversity**: JSON Schema's `format` keyword (`email`, `uri`, `uuid`, `date-time`, `ipv4`, `ipv6`) covers most common API constraints. Re-implementing each is wasteful when `Conjecture.Regex` already produces shrink-aware string strategies.
- **Schema scope**: full JSON Schema (all drafts, every keyword) is enormous. Most consumer schemas use a small core subset; supporting the long tail (`additionalProperties`, `patternProperties`, `if`/`then`/`else`, `dependentSchemas`) adds parser and generator complexity disproportionate to value.

## Decision

Ship schema-driven generation as **three separate satellite packages**, all producing `Strategy<JsonElement>`, all loading schemas at runtime, all delegating format-string constraints to `Conjecture.Regex`.

### Package layout

| Package | Entry point | Depends on |
|---|---|---|
| `Conjecture.JsonSchema` | `Generate.FromJsonSchema(...)` | `Conjecture.Core`, `Conjecture.Regex` |
| `Conjecture.OpenApi` | `Generate.FromOpenApi(...)` | `Conjecture.JsonSchema`, `Microsoft.OpenApi` |
| `Conjecture.Protobuf` | `Generate.FromProtobuf<T>()` | `Conjecture.Core`, `Google.Protobuf` |

Splitting per format avoids forcing OpenAPI consumers to take a Protobuf dependency (and vice versa), and matches the existing satellite pattern (`Conjecture.Time`, `Conjecture.Money`, `Conjecture.Regex`).

### Output type

All three packages produce `Strategy<JsonElement>`. No codegen path for strongly-typed C# outputs in v1; users who want a typed object call `JsonSerializer.Deserialize<T>(element)` themselves. This keeps the packages reflection-free at the schema-handling layer (the user-side `Deserialize<T>` is their decision, not ours), avoids a source-generator dependency, and sidesteps the AOT/trim implications of dynamic type construction.

### Schema loading

Runtime, async, multi-overload:

- `Generate.FromJsonSchema(string json)` / `(JsonElement)` / `(FileInfo)` / `(Uri)` (async for I/O variants).
- `Generate.FromOpenApi(FileInfo)` / `(Uri)` returning an `OpenApiDocument` wrapper exposing `RequestBody(method, path)`, `ResponseBody(method, path, statusCode)`, `PathParameter(...)`, `QueryParameter(...)`.
- `Generate.FromProtobuf<TMessage>()` and `Generate.FromProtobuf(MessageDescriptor)`.

No compile-time source generator in v1 — schemas typically live outside the project (S3, schema registries, version-controlled OpenAPI bundles), and runtime loading is more flexible without forcing a build-time fetch.

### `$ref` cycles

Resolved through `Generate.Recursive<JsonElement>` with `maxDepth = 5` default, configurable per call. Avoids infinite recursion while still producing realistically deep nested instances. The depth is a per-strategy knob, not a global setting, so different schemas can pick different bounds.

### Format constraints

JSON Schema `format` values delegate to existing `Conjecture.Regex` strategies:

| Format | Strategy |
|---|---|
| `email` | `Generate.Email()` |
| `uri`, `uri-reference` | `Generate.Url()` |
| `uuid` | `Generate.Uuid()` |
| `date-time`, `date` | `Generate.IsoDate()` |
| `ipv4` | `Generate.Ipv4()` (added in #477) |
| `ipv6` | `Generate.Ipv6()` (added in #477) |
| `time` | covered by `Generate.IsoDate()` time-of-day variant in #477 |
| arbitrary `pattern` | `Generate.Matching(pattern)` |

Unknown `format` values fall back to plain string generation honouring `minLength`/`maxLength` only.

### JSON Schema scope (v1)

Supported keywords (Draft 2020-12 subset):

- `type` — `null`, `boolean`, `integer`, `number`, `string`, `array`, `object` (and `type: [...]` arrays).
- Numeric: `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf`.
- String: `minLength`, `maxLength`, `pattern`, `format`.
- Array: `items`, `minItems`, `maxItems`, `uniqueItems`.
- Object: `properties`, `required`.
- Generic: `enum`, `const`, `$ref` (local pointers + `$defs`).
- Composition: `oneOf`, `anyOf`, `allOf`.

Explicitly **deferred** to follow-up issues:

- `additionalProperties`, `patternProperties`, `propertyNames`, `dependentSchemas`.
- `if`/`then`/`else`, `not`, `contains`, `unevaluatedProperties`/`unevaluatedItems`.
- Cross-document `$ref` (only local `#/...` and `$defs` pointers in v1).
- Negative / invalid-data generation (`SchemaGenerationMode.Invalid`).
- Schema evolution checks (v2 schema accepts v1-generated data).
- GraphQL schemas — smaller .NET user base; revisit if demand emerges.
- Auth schemas (OAuth scopes, API keys) — generating credentials is out of scope; we generate request/response bodies only.

### Constrained generation

Schema bounds bake into the IR draws themselves: `data.NextInteger(min, max)`, `data.NextString(minLength, maxLength)`, `data.NextSize(minItems, maxItems)`. No rejection sampling layer, no separate constrained-shrink pass — because the bounds are part of the draw, every shrunk choice-sequence still satisfies the schema by construction. Users get meaningful counter-examples (a JSON document at the boundary of `maxLength`, not "the empty string that happens to satisfy the filter").

### MCP integration

New `suggest-strategy-from-schema` tool in `Conjecture.Mcp` (#444). Kept separate from the existing `suggest-strategy` rather than overloading a single tool — the prompts and required parameters differ enough (schema source, endpoint selection, status-code selection) that one focused tool per scenario produces better LLM behaviour. The tool accepts an `adapter` parameter (`xunit-v3` | `nunit` | `mstest` | `testingplatform` | `expecto` | `interactive` | `linqpad`) so the scaffolded `[Property]` test matches the consumer's framework.

## Consequences

Positive:

- **Modular surface**: OpenAPI consumers do not pay for Protobuf, and vice versa.
- **Shrink-correct by construction**: bounds in the IR mean every counter-example minimises within the schema's valid space.
- **Reuses `Conjecture.Regex`**: format-aware string generation, IR-native shrinking, ASCII-by-default Unicode — all already solved.
- **AOT-friendly schema layer**: no `JsonSerializer.Deserialize<T>` happens inside our generators; users opt in to typed deserialisation.
- **Predictable scope**: a small JSON Schema subset covers the vast majority of consumer schemas; the deferred long tail can be added in follow-ups without breaking changes.
- **`$ref` cycles handled correctly**: `Generate.Recursive` already exists and shrinks toward the base case.

Negative:

- Three separate packages mean three separate `PublicAPI.Unshipped.txt` files, three separate test projects, three separate scaffolds. More upfront cost than a single umbrella.
- Strongly-typed output (`Strategy<TMessage>` for Protobuf, `Strategy<MyDto>` for JSON Schema) is a notable miss for users who want IntelliSense on the generated value. Mitigated by the trivial `JsonSerializer.Deserialize<T>` glue, revisited if user feedback demands it.
- Deferring `additionalProperties` means schemas relying on it will under-generate (no extra fields appear). Documented as a v1 limitation.
- Dual-validation gap: we generate from our subset, but a real consumer might validate against the full schema. If the schema uses an unsupported keyword, our generated values may technically satisfy our subset interpretation while violating the consumer's stricter validator. Mitigated by documenting the supported-keyword set explicitly per package.

## Alternatives Considered

**Single `Conjecture.Schema` umbrella package** — rejected: forces unwanted transitive dependencies on every consumer, and bundles the long-tail GraphQL/Avro support we may add later under one ABI. Splitting per format keeps each satellite focused and evolvable.

**Strongly-typed codegen via source generator** (`[FromOpenApi("swagger.json")] partial class Api { Strategy<CreateOrder> CreateOrder; }`) — rejected for v1: scope creep, AOT/trim implications, build-time dependency on schema files, and `JsonElement` is sufficient for the property-test scenario. Revisit as a follow-up `Conjecture.Schema.Generators` if user demand emerges.

**`NJsonSchema` for parsing** — rejected: we own the subset semantics anyway (because constrained generation requires us to interpret bounds, not just validate them), and `NJsonSchema`'s full surface exceeds our scope and pulls in additional deps.

**Compile-time schema loading** — rejected: schemas commonly live outside the project (object storage, schema registries), and pinning them at build time is brittle. Runtime loading with caching is more flexible.

**Generating invalid data alongside valid data in v1** — rejected: doubles the API surface (`SchemaGenerationMode.Valid`/`Invalid`/`Both`) and complicates shrinking semantics (what does "minimal invalid example" mean — minimal violation, or minimal distance from valid?). Deferred to a follow-up issue with its own ADR.

**Reusing the existing `suggest-strategy` MCP tool with an extra `schema` parameter** — rejected: the prompts and required arguments diverge enough that a focused `suggest-strategy-from-schema` tool produces sharper LLM output. Both tools can share helper code internally without sharing the public tool surface.

**`Generate.Ipv4`/`Ipv6` in a new `Conjecture.Net` satellite** — deferred to #477's own discussion. Default direction is to add them to `Conjecture.Regex` (where the format-string strategies already live) unless that issue surfaces a reason to split.
