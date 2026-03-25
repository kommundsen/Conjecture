# 0001. Version Number Tooling

**Date:** 2026-03-25
**Status:** Accepted

## Context

The project needs automated version number generation for NuGet packages. Two established tools exist in the .NET ecosystem: `MinVer` and `Nerdbank.GitVersioning` (NBGV). Both derive version numbers from git history, eliminating manual version management.

## Decision

Use **MinVer**.

## Consequences

- Version numbers are derived from git tags (e.g., `v1.2.3`). Tag a commit → `dotnet pack` produces the correct NuGet version with no additional config.
- Pre-release versions between tags are automatically suffixed (e.g., `1.2.3-alpha.0.5`).
- No `version.json` or other config file required.
- Works identically on developer machines and CI without environment-specific setup.

## Alternatives Considered

**Nerdbank.GitVersioning (NBGV):**
- Requires a `version.json` file and the `nbgv` CLI tool
- Adds build-height suffixes (`1.2.3.45`) useful for internal feed versioning but unnecessary for a public NuGet package
- Better suited for teams that need fine-grained control over pre-release labels across multiple branches
- Overkill for Conjecture.NET's simple tag-based release model
