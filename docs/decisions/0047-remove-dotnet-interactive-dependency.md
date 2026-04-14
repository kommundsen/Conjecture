# 0047. Remove Microsoft.DotNet.Interactive Dependency

**Date:** 2026-04-14
**Status:** Accepted
**Supersedes:** [0045](0045-notebook-integration-architecture.md)

## Context

Microsoft deprecated .NET Interactive on April 24, 2026 and Polyglot Notebooks on March 27, 2026 (dotnet/interactive#4163). No first-party replacement Jupyter kernel exists for .NET. Microsoft recommends file-based apps for interactive C# experimentation.

`Conjecture.Interactive` depended on `Microsoft.DotNet.Interactive` for `IKernelExtension` auto-registration and `Formatter.Register()`. However, only one source file (`ConjectureKernelExtension.cs`) used these APIs — the five other files (Preview, SampleTable, Histogram, ShrinkTrace, SvgHistogram) were pure string generation with zero Interactive dependency.

The HTML/SVG output format was designed for notebook cell rendering. Without a notebook host, users had no convenient way to view the output.

## Decision

Remove the `Microsoft.DotNet.Interactive` dependency and switch all output from HTML/SVG to plain text.

- **Delete** `ConjectureKernelExtension`, `StrategyHtmlFormatter`, and `extension.dib` — the Polyglot Notebooks integration surface.
- **Keep** all visualization extension methods on `Strategy<T>` with unchanged signatures.
- **Replace** HTML tables with box-drawing text tables and SVG histograms with block-character bar charts.
- **Rename** `SvgHistogram` → `TextHistogram` and `ShrinkTraceResult<T>.Html` → `.Text`.
- **Keep** the project name `Conjecture.Interactive` — it describes the exploration workflow, not the framework.

## Consequences

**Easier:**
- No dependency on a deprecated package that may break with future .NET SDKs.
- Text output works in any context: terminals, file-based apps, CI logs, test output.
- Simpler dependency graph — the project now only depends on `Conjecture.Core`.

**Harder:**
- Breaking change for any consumers using `ConjectureKernelExtension` or `SvgHistogram` directly.
- Users who relied on HTML/SVG output for embedding in web pages must generate their own markup.
- No automatic formatter registration in any notebook environment.

## Alternatives Considered

**Keep the dependency and wait for a community fork** — rejected; no fork has materialised, and depending on a deprecated package with known security policy gaps is untenable.

**Target a replacement Jupyter kernel** — rejected; no viable .NET Jupyter kernel exists outside of .NET Interactive itself.

**Fold visualization methods into Core** — rejected; same rationale as ADR 0045: Core must remain AOT-safe and trim-compatible without HTML/text rendering logic.
