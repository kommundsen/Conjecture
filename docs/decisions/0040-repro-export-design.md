# 0040. Repro Export Design

**Date:** 2026-04-09
**Status:** Accepted

## Context

When a property test fails, the developer needs to communicate that failure — to teammates,
in bug reports, or in CI logs — without requiring Conjecture to be installed on the recipient's
machine. Today, reproduction requires the same test code and a seed annotation. A standalone
`.cs` file with hard-coded counterexample values and a call to the original property makes the
failure self-contained, portable, and archivable.

Several design questions arose around how and when to generate the file, how to render
arbitrary .NET values as C# literals, which test framework to target, and how much metadata to
include.

## Decision

### Export trigger

Export is opt-in and automatic on failure, controlled by two new `ConjectureSettings` properties:

```csharp
bool ExportReproOnFailure { get; init; } = false   // default off
string ReproOutputPath { get; init; } = ".conjecture/repros/"
```

When `ExportReproOnFailure` is true, the framework-specific runner writes a `.cs` file
immediately after shrinking completes. A CLI `export` command is deferred to a later issue.

### Value rendering strategy

Counterexample values are rendered as C# literal declarations in this priority order:

1. **`FormatterRegistry`** — use `GetUntyped(Type)` for types with a registered formatter
   (covers `int`, `bool`, `double`, `float`, `string`, `byte[]`, `List<T>`, `HashSet<T>`,
   `Dictionary<K,V>`, tuples).
2. **JSON deserialization fallback** — for types without a registered formatter, serialize with
   `JsonSerializer.Serialize` and emit `JsonSerializer.Deserialize<T>("""...""")`.
3. **Non-serializable placeholder** — if serialization throws, emit:
   ```csharp
   // WARNING: SomeType cannot be serialized.
   // Value was: SomeType.ToString() result
   var name = default(SomeType)!;
   ```
4. **`null`** — emit `var name = (TypeName)null!;`.

### Code generation approach

Files are generated with `StringBuilder` string templates, consistent with the existing
`TestScaffoldingTools` in `Conjecture.Mcp`. Roslyn `SyntaxFactory` is not used — the template
structure is fixed and well-understood; the added 10 MB dependency is not justified.

### Framework inference

Each framework-specific runner emits the correct test attributes from its own context:

- `Conjecture.Xunit` → `[Fact]` / `using Xunit`
- `Conjecture.NUnit` → `[Test]` / `using NUnit.Framework`
- `Conjecture.MSTest` → `[TestMethod]` / `using Microsoft.VisualStudio.TestTools.UnitTesting`

No assembly scanning or `ConjectureSettings` framework enum is needed.

### Seed always included

The reproduction seed is included both as a header comment and as a `[Property(Seed = 0x...)]`
attribute, enabling direct re-run via Conjecture without the exported file:

```csharp
// Seed: 0xABCD1234
[Property(Seed = 0xABCD1234)]
[Fact]
public void Counterexample() { ... }
```

### Platform header

OS, .NET runtime version, and architecture are always written as header comments:

```csharp
// Platform: Windows 11 x64, .NET 9.0.3
```

This aids diagnosis of platform-specific failures without adding size to the actual test code.

### Exported content

The generated method:
- Declares each shrunk parameter as a typed variable using the value renderer
- Calls the original property method: `new TestClass().PropertyMethod(a, b)`
- Includes a `// NOTE:` comment for test classes without a parameterless constructor
- Detects `async` from `MethodInfo.ReturnType` and emits `async Task` + `await` accordingly

### Scope exclusions

- No `.csproj` is generated alongside the `.cs` — the file is designed to be dropped into an
  existing test project.
- MCP tool integration (`export-repro`) is deferred to a future issue.
- Version-control guidance is provided as a comment in the file header; Conjecture takes no
  stance on whether to commit or ignore repro files.

## Consequences

**Easier:**
- Failures are immediately shareable without any Conjecture knowledge on the recipient's side.
- The opt-in default (`false`) means no performance or I/O overhead for users who don't enable it.
- Each framework package owns its export — no cross-package coupling.
- The `StringBuilder` approach keeps `Conjecture.Core` free of Roslyn.

**Harder:**
- Non-serializable types produce placeholder code that won't compile without manual edits.
- Complex constructor graphs that `System.Text.Json` can't round-trip will fall back to the
  placeholder, limiting usefulness for some domain types.
- Adding NUnit and MSTest export requires duplicating the runner hookup in each framework
  package (mitigated by the shared `ReproFileBuilder` in Core).

## Alternatives Considered

**CLI-only export** — requires the developer to run a separate command after a failure; less
discoverable and misses the "automatic capture" use case. Deferred, not dropped.

**Roslyn `SyntaxFactory`** — produces guaranteed-valid AST output but adds ~10 MB of
`Microsoft.CodeAnalysis` to `Conjecture.Core`. Overkill for a fixed template.

**`IReproFormatter<T>` extensibility point** — a new interface parallel to `IStrategyFormatter<T>`
for registering custom literal renderers. Deferred: the JSON fallback covers most cases, and the
interface can be added without breaking changes later.

**Auto-detect framework by scanning loaded assemblies** — fragile when multiple frameworks are
present; unnecessary given each runner already knows its own framework.

**Generate `.csproj` alongside `.cs`** — adds scope for a marginal gain; most users already
have a test project to drop the file into.
