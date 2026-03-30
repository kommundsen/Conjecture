# 0029. Source Generator Architecture

**Date:** 2026-03-30
**Status:** Accepted

## Context

ADR-0010 decided to use a Roslyn incremental source generator triggered by `[Arbitrary]` to emit `Arbitrary<T>` implementations at compile time. This ADR specifies the concrete design: where `[Arbitrary]` lives, what the generated type is named, which type shapes are supported, how the generated code calls the existing `Gen.*` / `Strategies.*` API, what diagnostics are emitted for unsupported shapes, and why partial class is required.

## Decision

### Attribute placement

`[Arbitrary]` is a marker attribute defined in `Conjecture.Core` (not in `Conjecture.Generators`). This keeps the runtime dependency minimal: a project that only wants to annotate types does not pull in Roslyn tooling. The generator package (`Conjecture.Generators`) is referenced as a development-time-only dependency that reads the attribute from Core.

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class ArbitraryAttribute : Attribute { }
```

### Generated type naming

For a type `T`, the generator emits `{TypeName}Arbitrary` in the same namespace. Examples:

- `Point` → `PointArbitrary`
- `MyApp.Models.Order` → `MyApp.Models.OrderArbitrary`

The generated type implements `IStrategyProvider<T>` (ADR-0028) so it can be used directly with `[From<PointArbitrary>]` and is auto-discovered by `SharedParameterStrategyResolver` (Phase 3.5 / Phase 3.9).

Generated type is `internal sealed` and `partial` to allow user-provided overrides.

### Supported type shapes

| Shape | Constructor used | Emission pattern |
|---|---|---|
| `partial record` with primary constructor | Primary constructor | `new T(ctx.Next(s1), ...)` |
| `partial class` with single public constructor | That constructor | `new T(ctx.Next(s1), ...)` |
| `partial class` with multiple public constructors | Constructor with most parameters | `new T(ctx.Next(s1), ...)` |
| `partial struct` with accessible constructor | Same as class | `new T(ctx.Next(s1), ...)` |
| `partial class` / `partial struct` with init-only properties and no suitable constructor | All init properties | `new T { P1 = ctx.Next(s1), ... }` |

### Member type map

| C# type | Generated strategy expression |
|---|---|
| `int` | `Gen.Integers<int>()` |
| `long` | `Gen.Integers<long>()` |
| `byte` | `Gen.Integers<byte>()` |
| `float` | `Gen.Floats()` |
| `double` | `Gen.Doubles()` |
| `bool` | `Gen.Booleans()` |
| `string` | `Gen.Strings()` |
| `List<E>` | `Gen.Lists(«E strategy»)` |
| `T?` (nullable value type) | `Gen.Integers<T>()` with nullable wrapper |
| Enum type | `Gen.Integers<int>().Select(i => (TEnum)i)` |
| Type decorated with `[Arbitrary]` | `new {TypeName}Arbitrary().Create()` |
| Anything else | CON202 diagnostic; member skipped |

### Incremental pipeline

The generator uses `SyntaxProvider.ForAttributeWithMetadataName("Conjecture.ArbitraryAttribute", ...)` so only types marked `[Arbitrary]` trigger generation. The pipeline stages are:

1. **Collect** — find all `[Arbitrary]`-annotated `TypeDeclarationSyntax` nodes.
2. **Extract** — transform each `INamedTypeSymbol` into an immutable `TypeModel` record (namespace, type name, type kind, constructor parameters / init properties).
3. **Validate** — check for CON200/CON201; emit diagnostics and exit early on error.
4. **Emit** — render `{TypeName}Arbitrary` source text from `TypeModel`; register as additional source.

Using immutable `TypeModel` records as the intermediate representation satisfies the incremental generator requirement that transform outputs be value-comparable (for cache invalidation).

### Partial class requirement

The annotated type **must** be `partial`. The generator emits a `partial class/struct/record` companion alongside the user's declaration. If the type is not partial, the generator emits **CON201** (Error) and produces no output.

This is intentional: requiring `partial` makes the generated code visible and auditable, and signals to the developer that the type is a code-generation target.

### Diagnostics

| ID | Severity | Condition |
|---|---|---|
| CON200 | Error | No accessible constructor and no init-only properties; cannot emit strategy |
| CON201 | Error | Type decorated with `[Arbitrary]` is not `partial` |
| CON202 | Warning | Member type not in the type map and not itself `[Arbitrary]`-annotated; member is skipped in generated strategy |

Diagnostic IDs are stable per ADR-0004 SemVer policy.

### Emission pattern

Generated output uses only the existing trim-safe public API (ADR-0014):

```csharp
internal sealed partial class PointArbitrary : IStrategyProvider<Point>
{
    public Strategy<Point> Create() =>
        Strategies.Compose<Point>(ctx => new Point(
            ctx.Next(Gen.Integers<int>()),
            ctx.Next(Gen.Integers<int>())));
}
```

No reflection, no `Activator.CreateInstance`, no `Type.GetMembers()` in the generated output.

### Project targeting

`Conjecture.Generators` targets `netstandard2.0` — a Roslyn requirement for analyzer/generator projects. The generated *output* targets whatever the consuming project targets (net10.0 etc.). No .NET 10 APIs may be used inside generator source code itself.

## Consequences

- `[Arbitrary]` in Core keeps the attribute visible to all consumers without a Roslyn tooling dependency.
- `{TypeName}Arbitrary` naming is predictable and derivable from the type name alone; no registration table needed.
- The partial-class requirement makes generation opt-in and auditable, but adds one keyword to user type declarations.
- Supporting only records/classes/structs with accessible constructors or init properties excludes unusual shapes (abstract types, interfaces, types with only private constructors); CON200 guides users to fix or use `[From<T>]` manually.
- `netstandard2.0` targeting restricts generator source code to older APIs, but the generated output is unrestricted.
- The immutable `TypeModel` intermediate representation satisfies incremental-rebuild caching requirements and makes the extraction/emission pipeline independently testable.

## Alternatives Considered

- **`[Arbitrary]` in `Conjecture.Generators`**: Would require consuming projects to reference the generator package at runtime just to annotate types. Rejected; Core placement keeps the annotation cheap.
- **`{TypeName}Strategy` naming**: Collides more often with user-written strategies. `{TypeName}Arbitrary` matches the attribute name and is less likely to conflict.
- **Non-partial generated types (separate file, no `partial`)**: No `partial` companion means no user-override path. Rejected; partial is the Roslyn convention and enables extensibility.
- **Runtime fallback for unsupported types**: Silently emit reflection-based code for complex types. Rejected — breaks NativeAOT (ADR-0014) and defers errors to test time.
- **T4 / source-level code generation scripts**: No incremental rebuild, poor IDE integration, requires separate toolchain step. Rejected (same rationale as ADR-0010).
