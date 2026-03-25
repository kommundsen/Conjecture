# 0024. Example Database and Seed Reproduction Strategy

**Date:** 2026-03-25
**Status:** Accepted

## Context

When Conjecture.NET finds a failing example it must ensure that exact failure is re-run on every subsequent test run — regression prevention is a core value of the library. ADR-0012 decided the database *format* (SQLite in `.hypothesis/`). This ADR decides the *strategy*: whether to commit the database to source control, whether to print a seed for manual reproduction, and how the two mechanisms interact.

Without a clear strategy, teams face a common trap: a failure is found locally, the developer fixes it, but CI never replays the original buffer and re-discovers it only sporadically. Conversely, always requiring a committed database file creates friction for ephemeral CI environments and open-source contributors.

## Decision

Support both mechanisms; default to the committed database.

**1. Example database (primary)**

On failure, write the raw `ConjectureData` buffer to the SQLite database in `.hypothesis/examples/`. The test ID key is a stable hash of the fully-qualified test name (namespace + class + method + parameter types). On subsequent runs, the engine replays all stored buffers for a test *before* generating new random cases.

The `.hypothesis/` directory **should be committed to source control**. This ensures:
- CI re-runs known failing buffers without re-discovering them
- Team members share regression coverage automatically
- A fix can be verified by confirming the stored buffer no longer fails

SQLite WAL mode is used for safe concurrent access (ADR-0017).

The database can be disabled per-test or globally:

```csharp
// Global (settings.json or ConjectureSettings)
ConjectureSettings { UseDatabase = false }

// Per-test
[Property(UseDatabase = false)]
```

Disabling is appropriate for flaky-test isolation or fully ephemeral CI runners where the workspace is discarded after every run.

**2. Seed-based reproduction (secondary)**

The seed is always printed on failure alongside the counterexample (ADR-0022):

```
Reproduce with: [Property(Seed = 0xDEADBEEF)]
```

Users paste this attribute to reproduce the failure locally without needing the database file. When a `Seed` is set on a `[Property]`, the engine uses that seed exclusively and skips the database — deterministic replay takes precedence.

## Consequences

- Teams get regression prevention by default without any configuration; committed `.hypothesis/` is analogous to committed test fixtures
- The `.gitignore` default for new .NET projects does *not* ignore `.hypothesis/`; documentation must make the commit recommendation explicit
- Seed printing makes cross-machine reproduction trivial even when the database is unavailable or ignored
- Database entries for deleted or renamed tests accumulate silently; a periodic `hypothesis clean` CLI command (future work) would prune orphaned entries
- Seed-only mode (`UseDatabase = false`) degrades CI to re-discovery mode — teams must manually track seeds if they want regression prevention without a committed database

## Alternatives Considered

- **Database only, no seed printing** — harder to reproduce locally when `.hypothesis/` is in `.gitignore` or the contributor is working from a different machine; poor onboarding experience
- **Seed only, no database** — requires manually adding `[Property(Seed = ...)]` after every failure; CI re-discovers known failures on every run unless seeds are separately committed and plumbed back into attributes; more friction than a committed directory
- **Remote/shared database** — a networked cache shared across CI runners and developer machines; eliminates the committed-file requirement but adds infrastructure dependency and auth complexity; out of scope for v1
