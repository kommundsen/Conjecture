# 0046. Analyzer Suite Expansion (CON107–CON112)

**Date:** 2026-04-12
**Status:** Accepted

## Context

Conjecture ships a Roslyn analyzer package (`Conjecture.Analyzers`) that catches common property-test mistakes at compile time. The initial suite covered ID range CON101–CON106. A planned expansion to CON107–CON112 was designed to catch additional categories of misuse: non-determinism, unreachable filters, missing strategies, async pitfalls, misplaced targeting calls, and type-unsafe composition chains.

Before implementing the new analyzers, two structural decisions needed to be made:

1. **CON106 disposition** — CON106 was originally slated to warn on a specific pattern, but analysis showed it overlaps entirely with CON101 (incorrect usage of the core entry point) at constant-analysis depth. Shipping a redundant diagnostic would produce duplicate warnings with no additional signal.

2. **Diagnostic ID reservation** — As the suite grows, IDs must be stable and non-colliding across core diagnostics and suggestion-class diagnostics.

3. **Severity policy** — Users must be able to silence or escalate any individual diagnostic via standard .editorconfig rules without custom Conjecture infrastructure.

4. **Test infrastructure** — The existing analyzer tests use ad-hoc string compilation helpers. The `Microsoft.CodeAnalysis.Testing` framework provides a maintained, high-signal harness designed specifically for Roslyn analyzers and code fixes.

## Decision

**CON106 is dropped.** It is fully subsumed by CON101 at the depth of constant analysis Conjecture performs. Adding it would emit two warnings for the same root cause.

**CON107–CON112 are added with the following severities:**

| ID | Name | Severity | Rationale |
|----|------|----------|-----------|
| CON107 | Non-deterministic operation in [Property] | Warning | Breaks shrink reproducibility; almost always a mistake |
| CON108 | Unreachable Assume.That with known provider | Warning | Fires only on built-in providers (PositiveInts, NegativeInts, NonEmptyString, etc.) where the value space is statically known; silent on custom providers to avoid false positives |
| CON109 | Missing strategy for [Property] parameter type | Warning | Detected via semantic type resolution at the parameter site; unresolvable type means the test cannot run |
| CON110 | Async [Property] without await | Info | CS1998 already covers the general case; Conjecture re-surfaces it with property-test context so the message points to the right fix |
| CON111 | Target.Maximize/Minimize outside [Property] | Warning | Targeting calls outside a property test are no-ops and indicate copy-paste errors |
| CON112 | Strategy composition type mismatch in Select chain | Error | Type-safety violation; the chain produces the wrong type at runtime |

**ID reservation:** CON106–CON199 are reserved for core Conjecture diagnostics. CJ0XXX is reserved for suggestion-class diagnostics (style, non-functional recommendations).

**Severity overrides** use the standard `dotnet_diagnostic.CONXXX.severity = none|suggestion|warning|error` mechanism in `.editorconfig`. No custom suppression infrastructure is introduced.

**Test infrastructure** for `Conjecture.Analyzers.Tests` is migrated to `Microsoft.CodeAnalysis.Testing` (see sub-issue [#166](https://github.com/kommundsen/Conjecture/issues/166)) before the new analyzers are implemented. This provides inline diagnostic markers, automatic fixup verification, and a maintained upgrade path as Roslyn evolves.

## Consequences

- **Easier:** Each new analyzer gets a well-specified ID, severity, and test harness. Adding future diagnostics follows the same pattern.
- **Easier:** Users can tune severity per-diagnostic without any Conjecture-specific configuration surface.
- **Harder:** CON108 requires maintaining a list of "known built-in providers" inside the analyzer; this list must be updated when new built-ins are added.
- **Harder:** CON109 requires semantic analysis (symbol resolution), which is slower than syntax-only analysis. This is acceptable given the value of the diagnostic.
- **Neutral:** Dropping CON106 leaves a gap in the ID sequence. This is intentional — IDs are stable identifiers, not a contiguous range, and the gap signals that CON106 was evaluated and consciously omitted.

## Alternatives Considered

**Keep CON106 as a distinct diagnostic.** Rejected — it fires on the exact same source locations as CON101 and adds noise without signal. Users would need to suppress two IDs for the same issue.

**Use a custom severity configuration API** (e.g., `[assembly: ConjectureDiagnosticSeverity(...)]`). Rejected — the standard `dotnet_diagnostic` mechanism is already understood by every .NET developer and every linter integration. Introducing a parallel system adds surface area with no benefit.

**Retain the ad-hoc test compilation helpers.** Rejected — `Microsoft.CodeAnalysis.Testing` is the community standard, is actively maintained by the Roslyn team, and significantly reduces test boilerplate for inline diagnostic assertions and fix verification.

**Use CJ0XXX for all diagnostics.** Rejected — the CON prefix is already established in the public API surface and documentation. Mixing prefixes for the same package would be confusing. CJ0XXX is reserved for a future suggestion tier if needed.
