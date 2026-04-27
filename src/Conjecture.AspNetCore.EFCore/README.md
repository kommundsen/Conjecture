# Conjecture.AspNetCore.EFCore

Satellite package that wires together `Conjecture.AspNetCore` and `Conjecture.EFCore` for property-based integration testing of ASP.NET Core applications backed by Entity Framework Core.

Provides `AspNetCoreDbTarget<TContext>`, which combines `WebApplicationFactory<TEntryPoint>` with an EF Core `DbContext` to support three invariants:

- **Roundtrip** — entities written during a request survive a re-read.
- **Migration** — the schema produced by `EnsureCreated` matches `Database.Migrate`.
- **Snapshot** — before/after `EntitySnapshot` diffs validate observable side-effects.

See [ADR 0066](../docs/decisions/0066-conjecture-aspnetcore-efcore-package-design.md) for design rationale.