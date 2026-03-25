# 0003. Breaking Change CI Gating

**Date:** 2026-03-25
**Status:** Accepted

## Context

PublicApiAnalyzers (ADR 0002) catches API surface changes at development time, but has blind spots: generic constraint changes, implicit interface contract removals, and attribute changes that affect binary compatibility can slip through. A CI gate comparing the built assembly against the last published baseline provides a second line of defense.

## Decision

Use **Microsoft.DotNet.ApiCompat** in CI to compare built assemblies against a committed baseline generated from the last published NuGet package.

The baseline (`api-baseline.txt`) is regenerated and committed when a new version is tagged. On every PR, CI compares the PR's build against this baseline and fails if breaking changes are detected without a corresponding major version increment.

```xml
<!-- Conjecture.Core.csproj -->
<PropertyGroup>
  <ApiCompatEnableRuleAttributesMustMatch>true</ApiCompatEnableRuleAttributesMustMatch>
  <ApiCompatBaseline>$(MSBuildThisFileDirectory)api-baseline.txt</ApiCompatBaseline>
</PropertyGroup>
```

## Consequences

- Breaking changes that evade code review and PublicApiAnalyzers are caught before merge.
- The baseline file must be updated as part of every release — this is the correct moment to do it (intentional, reviewed).
- Pre-1.0, the baseline is still maintained but failures are warnings rather than errors (configurable via `ApiCompatTreatIssuesAsErrors`). This preserves the "no stability guarantee before 1.0" policy without disabling the tooling entirely.

## Alternatives Considered

**Conventional Commits + semantic-release (automated version bumping):**
- Automates the decision of whether a change is breaking. Risky: the tool can misclassify, and an incorrectly bumped major version is confusing to users. Human judgment at tagging time is retained.

**NuGet Package Validation (`EnablePackageValidation`):**
- The SDK-built-in option. Less mature and configurable than ApiCompat; ApiCompat gives finer control over which rules are enforced and supports baseline suppression files.
