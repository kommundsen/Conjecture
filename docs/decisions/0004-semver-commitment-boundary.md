# 0004. SemVer Commitment Boundary

**Date:** 2026-03-25
**Status:** Accepted

## Context

The project will ship usable pre-release versions before the API stabilizes. Early adopters need access, but premature SemVer commitments constrain architectural iteration. The team needs a clear, documented policy on when stability guarantees begin and what they mean.

## Decision

- **`0.x` versions** (Phases 0–2): no API stability guarantee. Minor versions may contain breaking changes. This is explicitly communicated in the README and NuGet package description.
- **`1.0.0`**: API frozen. Strict SemVer begins:
  - Breaking changes → major version bump
  - New non-breaking features → minor version bump
  - Bug fixes → patch version bump
- The transition from `0.x` to `1.0.0` requires: full strategy library complete, xUnit integration stable, example database working, and documentation complete.

Enforcement tooling (ADRs 0002, 0003) is active throughout — even during `0.x` — so the discipline is practiced before it's contractually required.

## Consequences

- Early adopters can integrate and provide feedback without the project being locked in.
- The explicit `0.x` policy prevents complaints about breaking changes from users who didn't read the docs — it's opt-in instability.
- Running ApiCompat in warning mode during `0.x` (rather than error mode) preserves the freedom to break while still tracking the delta.
- `1.0.0` is a meaningful milestone with defined criteria, not an arbitrary tag.

## Alternatives Considered

**Start strict SemVer from `0.1.0`:**
- Unnecessarily constrains early design iteration. The API will change significantly as the engine and strategies are built out.

**Never formally commit to SemVer:**
- Acceptable for internal tooling; unacceptable for a public NuGet package targeting broad adoption. Library consumers need to be able to upgrade safely.
