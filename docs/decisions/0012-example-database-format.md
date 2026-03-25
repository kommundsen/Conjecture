# 0012. Example Database Format

**Date:** 2026-03-25
**Status:** Accepted

## Context

When Hypothesis finds a failing example, it shrinks it to a minimal counter-example and should persist it so that future test runs replay the failure immediately (without re-discovering it). The storage format for this example database affects portability, query performance, and ease of inspection.

## Decision

Use a SQLite-backed local database (one file per test suite, stored in a `.hypothesis/` directory) to persist shrunk counter-examples, keyed by a stable hash of the test identity.

## Consequences

- SQLite provides ACID guarantees; concurrent test runners writing to the same DB are safe without custom locking.
- The `.hypothesis/` directory can be committed to source control for team-wide sharing of known failing cases, or gitignored for local-only storage — the team decides.
- SQLite files are binary but widely inspectable via standard tooling (`sqlite3` CLI, DB Browser for SQLite).
- Adds a dependency on a SQLite ADO.NET provider (e.g., `Microsoft.Data.Sqlite`); this is a well-maintained, trim-safe package.
- Schema migrations must be handled carefully as the format evolves; a version table is required from day one.

## Alternatives Considered

- **Flat files (one file per test)**: Simple to implement and inspect, but no atomic multi-example updates and poor performance at scale.
- **JSON files**: Human-readable, but no transactions and fragile under concurrent writes.
- **In-memory only (no persistence)**: Eliminates the dependency but means every run must re-discover failures; defeats a key Hypothesis UX feature.
- **LiteDB / other embedded DBs**: Fewer users, less tooling support, and no meaningful advantage over SQLite for this use case.
