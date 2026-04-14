# 0045. Notebook Integration Architecture

**Date:** 2026-04-12
**Status:** Superseded by [0047](0047-remove-dotnet-interactive-dependency.md)

## Context

.NET Interactive (Polyglot Notebooks / `.dib`) is an increasingly common environment for exploratory data work and library evaluation. Developers discovering Conjecture benefit from being able to call `.Preview()` or `.Histogram()` on a strategy directly inside a notebook cell and see formatted output without writing a full test suite.

Conjecture.Core is designed to be AOT-safe and trim-compatible. Shipping notebook rendering code inside Core would pull in HTML/SVG string-building logic that defeats tree-shaking and complicates the AOT contract. A separate opt-in package sidesteps this entirely.

.NET Interactive provides `IKernelExtension` as the standard hook for auto-loading formatters when a NuGet package is referenced in a notebook. Without this, users would need to call a setup method manually in every notebook session.

## Decision

Ship notebook support as a standalone `Conjecture.Interactive` NuGet package, separate from `Conjecture.Core`.

- **Auto-registration** via `IKernelExtension` + `[assembly: KernelExtension]` — formatters activate automatically when the package is `#r`-referenced; no user setup call is required.
- **Static rendering only** — all output is HTML or SVG with no embedded JavaScript. A custom SVG renderer handles histograms; HTML tables handle sample and shrink-trace output.
- **Extension methods** (`.Preview()`, `.SampleTable()`, `.Histogram()`, `.ShrinkTrace()`) are defined in `Conjecture.Interactive` and target `Strategy<T>`. Console users continue to use `DataGen.Sample()` from Core; no duplication of sampling infrastructure.
- **Output caps**: `.Preview()` — max 100 samples; `.SampleTable()` — max 50 rows; `.Histogram()` — always aggregates (no row limit). A truncation notice is appended when the cap is hit.
- **Seed convention**: all extension methods accept an optional `ulong? seed` parameter (default `null` → random). This mirrors `DataGen.Sample()` so users transfer their mental model without friction.
- **One sample notebook** ships at `docs/notebooks/Conjecture-QuickStart.dib`.
- **F# support is deferred** until `Conjecture.FSharp` is stable; the Interactive package targets C# notebooks only for now.

## Consequences

**Easier:**
- Core remains trim-compatible and AOT-safe — no HTML/SVG logic bleeds in.
- Notebook users get zero-friction setup via the kernel extension convention.
- The output cap and truncation notice prevent accidental generation of enormous notebooks.

**Harder:**
- An additional NuGet package to version and publish alongside Core.
- F# notebook users have no out-of-the-box experience until the deferred work lands.
- The SVG histogram renderer must be maintained by hand (no third-party charting library to avoid JS dependencies).

## Alternatives Considered

**Include rendering in Core behind a compile flag** — rejected; compile flags complicate the AOT/trim contract and are opaque to package consumers.

**Use a JavaScript-based charting library** — rejected; JS in notebook output creates sandboxing and portability issues (some notebook hosts strip scripts). Static SVG works everywhere.

**Require explicit user registration (`ConjectureKernel.Register()`)** — rejected; the `IKernelExtension` pattern is the established convention in the .NET Interactive ecosystem and eliminates the most common source of "why isn't my formatter working?" questions.

**Expose `.Preview()` on Core's `Strategy<T>`** — rejected; it would force a dependency on `Microsoft.DotNet.Interactive` into Core, breaking trim and adding a large transitive closure for all consumers.
