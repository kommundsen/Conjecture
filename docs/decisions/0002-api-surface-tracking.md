# 0002. API Surface Tracking

**Date:** 2026-03-25
**Status:** Accepted

## Context

Contributors need to know when a change affects the public API. Without tooling, breaking changes can ship in minor versions by accident. The review process alone is insufficient — reviewers may not recognize subtle breakages (e.g., adding a required interface member, changing a return type, removing an overload).

## Decision

Adopt **Microsoft.CodeAnalysis.PublicApiAnalyzers** in all public-facing projects (`Conjecture.Core`, `Conjecture.Xunit`, etc.).

Two files are committed alongside each project:
- `PublicAPI.Shipped.txt` — API surface of the last released version
- `PublicAPI.Unshipped.txt` — additions/changes staged for the next release

Any modification to the public API that isn't reflected in these files **fails the build**. Developers must explicitly update the files, which surfaces API changes at PR review time.

At release: content of `Unshipped` moves to `Shipped`, files are committed, then tagged with MinVer.

## Consequences

- API changes are impossible to accidentally merge without reviewer visibility — the file diff makes them explicit.
- New contributors get an immediate build error (with a clear message) if they add a public member without updating the tracking file.
- The two-file model (`Shipped` / `Unshipped`) cleanly separates "what we've committed to" from "what's in progress".
- Pre-1.0, `Shipped.txt` remains empty — no stability guarantee is implied.

## Alternatives Considered

**Process-only (PR review + CONTRIBUTING.md rules):**
- Relies on human attention; silently fails when reviewers are unfamiliar with the full API surface.

**NuGet diff tooling (post-release comparison):**
- Catches breaking changes after they've shipped. Too late.

**ApiCompat alone:**
- CI-only; doesn't give developers local feedback during development. Complementary to PublicApiAnalyzers, not a replacement (see ADR 0003).
