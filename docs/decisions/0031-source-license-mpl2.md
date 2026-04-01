# 0031. Source License: MPL-2.0

**Date:** 2026-04-01
**Status:** Accepted (supersedes 0005)

## Context

Conjecture.NET was initially licensed under MIT (ADR-0005) as a clean-room implementation. As the project matures and begins porting algorithms directly from the MPL-2.0-licensed Python Hypothesis library, a unified licensing model is needed that:

- Protects source modifications via copyleft (encouraging contribution back)
- Maintains zero friction for the majority of users who consume NuGet packages
- Aligns with the upstream Hypothesis license (MPL-2.0)
- Eliminates the awkward per-file dual-license model from ADR-0026

## Decision

- **Source code** is licensed under MPL-2.0.
- **NuGet packages** (compiled form) are distributed under MIT.

MPL-2.0 §3.2(b) explicitly permits distributing Executable Form "under different terms, provided that the license for the Executable Form does not attempt to limit or alter the recipients' rights in the Source Code Form under this License."

All `.cs` files carry the MPL-2.0 notice header. `LICENSE.txt` at the repo root contains the full MPL-2.0 text. `LICENSE-MIT.txt` is bundled into NuGet packages.

## Consequences

- Enterprise NuGet consumers see MIT — no copyleft friction, auto-approved by license scanners.
- Anyone who forks and modifies the source must distribute those modifications under MPL-2.0.
- Contributors submit code under MPL-2.0 terms.
- The per-file MIT/MPL-2.0 distinction from ADR-0026 is eliminated — all source files are MPL-2.0.
- Companies that build from source (rather than consuming NuGet) will see MPL-2.0 in their scanners.
