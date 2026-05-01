# Why three packages instead of one

Schema-driven generation ships as three separate packages — `Conjecture.JsonSchema`, `Conjecture.OpenApi`, and `Conjecture.Protobuf` — rather than a single `Conjecture.Schema` umbrella. This page explains the reasoning.

## The dependency problem

The three schema formats require fundamentally different dependencies:

| Package | Key dependency | Size |
|---|---|---|
| `Conjecture.JsonSchema` | None (uses `System.Text.Json`) | Minimal |
| `Conjecture.OpenApi` | `Microsoft.OpenApi.Readers` | ~1 MB |
| `Conjecture.Protobuf` | `Google.Protobuf` | ~1.5 MB |

A project that tests a JSON Schema–validated API has no use for Protobuf reflection assemblies. A project that tests a gRPC service has no use for the OpenAPI parser. Bundling everything into one package forces every consumer to pull in dependencies they don't need.

This follows the same principle behind Conjecture's other optional packages: `Conjecture.Time`, `Conjecture.Money`, `Conjecture.Regex`, and `Conjecture.FSharp` each carry their own dependency footprint and ship separately.

## The API surface is independent

The three entry points — `Strategy.FromJsonSchema`, `Strategy.FromOpenApi`, and `Strategy.FromProtobuf` — each extend the same `Generate` class via C# extension methods. Adding one package to a project enables its entry point without touching the others. The extension method pattern means there's no base class or shared abstraction that would force a coupled release cycle.

## `Conjecture.JsonSchema` is the foundation

`Conjecture.OpenApi` is built on top of `Conjecture.JsonSchema`. OpenAPI schemas are JSON Schemas under the hood — the adapter parses the OpenAPI document, extracts the relevant `OpenApiSchema` objects, converts them to `JsonSchemaNode`, and hands off to the same `JsonSchemaStrategy` that the JSON Schema package uses directly.

This means JSON Schema constraint support improves automatically for OpenAPI users, without requiring a separate release of `Conjecture.OpenApi`.

`Conjecture.Protobuf` is independent — it converts Protobuf `FieldDescriptorProto` types directly to strategies rather than routing through JSON Schema, because Protobuf's type system (fixed-width integers, repeated fields, `oneof`, `bytes`) doesn't map cleanly to JSON Schema semantics.

## Versioning and stability

Keeping the packages separate means each can evolve at its own pace. OpenAPI support may stabilise before Protobuf support is complete. A breaking change to how `Microsoft.OpenApi` parses schemas does not require a new version of `Conjecture.JsonSchema`. And a project pinned to a specific version of `Google.Protobuf` is not forced onto a newer version by an unrelated OpenAPI fix.

## The tradeoff

The main cost is that a project testing both a REST API and a gRPC service needs two `dotnet add package` invocations instead of one. This is considered acceptable — the packages are independently useful, and the install experience for the common case (one format, one package) is simpler than a monolithic install.

## Further reading

- [How to property-test an HTTP API endpoint using OpenAPI](../how-to/test-http-api-with-openapi.md)
- [How to generate data from a JSON Schema definition](../how-to/generate-from-json-schema.md)
- [How to generate Protobuf message payloads](../how-to/generate-protobuf-payloads.md)
- [ADR 0060: Schema-driven generation package layout](../../decisions/0060-schema-driven-generation-package-layout.md)
