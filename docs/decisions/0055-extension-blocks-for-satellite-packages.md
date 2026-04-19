# 0055. Extension Blocks for Satellite Packages

**Date:** 2026-04-20
**Status:** Accepted

## Context

Conjecture ships satellite packages that extend the core generation API for specific domains:
`Conjecture.Time`, `Conjecture.Regex`, and planned additions such as `Conjecture.Money`,
FluentValidation, ASP.NET Core, and EF Core integrations.

Each satellite currently introduces its own parallel static factory class (`TimeGenerate`,
`RegexGenerate`), forcing users to learn and import a new symbol per package. The consequence
is fragmented discoverability: `Generate.Integer()` is on `Generate`, but `Generate.Matching()`
is on `RegexGenerate` — an entirely different type.

C# 14 `extension` blocks allow a satellite package to contribute static members directly onto
an existing static class (`Generate`) without modifying the owning assembly. The question is
how to formalise this as a cross-satellite convention before more packages start shipping.

## Decision

Each satellite package that contributes factory methods contributes them via one or more
`public static class <Name>Extensions` files containing `extension(Generate) { … }` blocks
(for factory methods) or `extension(Strategy<T>) { … }` blocks (for fluent combinators).
Private helpers stay on the containing `static class`.

**Namespace policy.** Core stays `Conjecture.Core`; satellites stay `Conjecture.<Area>`
(e.g. `Conjecture.Time`, `Conjecture.Regex`). Consumers add a package reference and a
`using Conjecture.<Area>;` directive — no `using static` or type qualification required.
The namespace hierarchy is _not_ the discovery mechanism; extension blocks are.

**Static vs. instance extensions.**
- Use `extension(Generate)` for factory methods that produce a new strategy from nothing
  (`Generate.TimeZones()`, `Generate.Matching(pattern)`).
- Use `extension(Strategy<T>)` for fluent combinators that transform an existing strategy
  (`strategy.NearMidnight()`, `strategy.NonEmpty()`).
- A satellite may contribute both kinds in the same package.

**Breaking-change posture.**
- Pre-1.0: parallel factories (`TimeGenerate`, `RegexGenerate`) are removed in v0.16.0
  without `[Obsolete]` forwarders. Users upgrading must switch to `Generate.*` calls.
- Post-1.0: deprecated factory classes get one release of `[Obsolete(DiagnosticId = "…")]`
  before removal.

**Precedent already in the codebase.**
- `StrategyExtensionProperties.cs` (Core) — `extension(Strategy<int>)`, `extension(Strategy<string>)`.
- `DateTimeOffsetExtensions.cs` (Time) — `extension(Strategy<DateTimeOffset>)`.

**Non-goals.**
- F# surface — stays in the `Conjecture` namespace per F# convention; extension blocks are
  not idiomatic in F# and the FSharp package wraps Core APIs separately.
- Analyzers / Generators targeting `netstandard2.0` — those assemblies cannot use C# 14
  language features regardless of the host compiler version.

## Consequences

- **Easier:** Adding a satellite package is enough to surface new `Generate.*` factories in
  IntelliSense without any extra `using static` or type alias. The extension-block pattern
  scales to arbitrarily many satellites without polluting the `Conjecture.Core` namespace.
- **Harder:** The pre-1.0 removal of `TimeGenerate` and `RegexGenerate` is a breaking change
  for consumers who imported those types directly. Migration is mechanical (find/replace), but
  must be communicated in the v0.16.0 changelog.
- C# 14 is required in the _consuming_ project to benefit from extension-block resolution.
  Projects on earlier language versions can still call satellite methods via explicit
  `<ClassName>.Method()` qualification, but this is not the advertised API shape.

## Alternatives Considered

**Keep parallel factory classes.** Rejected — causes permanent fragmentation. Every new
satellite adds a name users must discover separately (`TimeGenerate`, `RegexGenerate`,
`MoneyGenerate`, …). The whole value of a unified `Generate.*` surface is lost.

**Flat `Conjecture` namespace for all satellites.** Rejected — conflicts with F# packaging
convention and creates ambiguity between framework adapters (`Conjecture.Xunit`) and
generation-domain packages (`Conjecture.Regex`). Namespace hierarchy carries structural
meaning even if it is not the primary discovery path.

**Extension methods (C# ≤ 13 `this` parameter on a static class instance).** Not applicable
— C# does not allow extension methods on `static` classes via `this`. Extension blocks
(C# 14) are the first language mechanism that targets static classes.
