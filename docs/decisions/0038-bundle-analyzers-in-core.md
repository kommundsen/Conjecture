# 0038. Bundle Analyzers and Generator into Core Package

**Date:** 2026-04-05
**Status:** Accepted

## Context

ADR-0023 specified a separate `Conjecture.Analyzers` NuGet package for Roslyn diagnostics and code fixes. As the project approaches its first release (v0.6.0-alpha.1), the separate-package model adds friction: users who run `dotnet add package Conjecture.Core` get no analyzers and must discover and install a second package manually. For a pre-1.0 library aiming to build an audience, reducing the "zero to property test" setup cost matters more than the theoretical opt-out flexibility.

## Decision

Ship `Conjecture.Analyzers`, `Conjecture.Analyzers.CodeFixes`, and `Conjecture.Generators` DLLs inside `Conjecture.Core.nupkg` under `analyzers/dotnet/cs/`. The three projects are marked `IsPackable=false` so they do not ship as standalone packages. `Conjecture.Core.csproj` references them via `ProjectReference` with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`, which causes MSBuild to place the DLLs in the analyzers folder of the Core package at pack time.

## Consequences

- `dotnet add package Conjecture.Core` installs analyzers, code fixes, and the source generator with no extra steps
- The three analyzer/generator projects lose their standalone package identities; they become implementation details of Core
- Package size of Core increases slightly (the analyzer DLLs are small)
- Users cannot opt out of the analyzers without editing their `.editorconfig` or adding `<NoWarn>` entries — acceptable for development-time diagnostics

## Alternatives Considered

- **Separate `Conjecture.Analyzers` package (ADR-0023 approach)** — rejected; extra install step hurts first-use experience and the opt-out argument carries less weight pre-1.0
- **Opt-in via NuGet package tag / feature flag** — rejected; unnecessary complexity for a majority use case where all users benefit from the analyzers
