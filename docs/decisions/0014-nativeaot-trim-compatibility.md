# 0014. NativeAOT and Trim Compatibility

**Date:** 2026-03-25
**Status:** Accepted

## Context

.NET's NativeAOT compilation and IL trimming require that libraries avoid runtime reflection, dynamic code generation, and other patterns that cannot be statically analysed. Retrofitting trim-safety onto an existing codebase is significantly more expensive than designing for it from the start.

## Decision

Commit to a trim-safe and NativeAOT-compatible public API from day one. All code paths reachable from the public surface must be statically analysable. Reflection-dependent features (e.g., automatic `Arbitrary<T>` derivation) must be implemented via source generators (see ADR-0010) rather than runtime reflection.

## Consequences

- Users targeting NativeAOT (e.g., console tools, mobile, WASM) can use Conjecture.NET without publish-time warnings or runtime failures.
- The constraint rules out convenient but reflection-heavy shortcuts during implementation; every feature must be designed with static analysis in mind.
- `[RequiresDynamicCode]` and `[RequiresUnreferencedCode]` attributes must be applied to any intentionally unsafe escape hatches, making the boundary explicit.
- CI should include a NativeAOT publish step to catch regressions early.
- The source generator approach (ADR-0010) is load-bearing for this decision; if source generators are not viable for some type category, trim annotations are required as a fallback.

## Alternatives Considered

- **Retrofit later**: Defer trim-safety until 1.0. Lower initial cost but risks locking in reflection-dependent APIs that are hard to change without breaking changes.
- **Best-effort trim annotations only**: Annotate known-unsafe paths but don't enforce full compatibility. Leads to a confusing mix of safe and unsafe APIs with no clear contract.
