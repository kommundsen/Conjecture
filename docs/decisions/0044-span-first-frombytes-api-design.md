# 0044. Span-first Generate.FromBytes\<T\> API design

**Date:** 2026-04-12
**Status:** Accepted

## Context

`ConjectureData.ExportReproOnFailure` emits a `byte[]` buffer that encodes the exact draw sequence for a failing run. To support round-trip replay, callers need a way to construct a `ConjectureData` instance seeded from that buffer and then draw a value of type `T` — without re-running the full engine.

`ReadOnlySpan<byte>` is the idiomatic .NET API surface for read-only byte input. However, spans are ref structs and cannot be stored in fields, which affects internal strategy design. C# 14 adds implicit conversions from `byte[]` to `ReadOnlySpan<byte>` at call sites, reducing overload noise.

Several design questions needed resolution before implementation:

- Should `ConjectureSettings` or `IGeneratorContext` grow a span-accepting overload?
- Should a `byte[]` overload be added alongside the span overload?
- Should `Memory<byte>` be supported for async callers?
- Does the source generator need to handle `FromBytes` strategies?

## Decision

Implement `Generate.FromBytes<T>(ReadOnlySpan<byte>)` as the sole new entry point. The span is copied into a `byte[]` field inside `FromBytesStrategy<T>` on construction, so the strategy can be stored and composed like any other `IStrategy<T>`. No overloads accepting `byte[]`, `Memory<byte>`, or `IBufferWriter<byte>` are added.

`ConjectureSettings` and `IGeneratorContext` are unchanged. The span accessor on `IGeneratorContext` is deferred to a future issue once the usage pattern is better understood.

Callers on C# 14+ benefit from the implicit `byte[]` → `ReadOnlySpan<byte>` conversion; earlier language versions must call `.AsSpan()` explicitly (or await the language upgrade).

The source generator is unaffected: `FromBytesStrategy<T>` is an ordinary closed generic and requires no special code-gen support.

## Consequences

- **Replay is possible** end-to-end: capture bytes via `ExportReproOnFailure`, pass to `Generate.FromBytes<T>`, run a single-case property.
- **One defensive copy per construction** — acceptable for a replay path that is not on the hot draw path.
- **No `byte[]` overload** means callers on C# ≤ 13 must call `.AsSpan()`. This is intentional; adding a parallel `byte[]` overload would double the API surface permanently for a convenience that disappears with the C# 14 implicit conversion.
- **`IGeneratorContext` unchanged** keeps the context interface stable and avoids committing to a span-based draw API before the semantics are clear.
- **`Memory<byte>` deferred** is consistent with Conjecture's synchronous-only property model; an async path would require broader engine changes.

## Alternatives Considered

**Add a `byte[]` overload alongside the span overload.** Rejected: the implicit conversion in C# 14 makes it redundant, and it would create permanent API surface to maintain and document.

**Add a span overload to `IGeneratorContext`.** Deferred: the interface is stable and widely implemented. Changing it requires all adapter projects to update. The use case is not yet clear enough to justify the churn.

**Use `Memory<byte>` instead of `ReadOnlySpan<byte>`.** Rejected: `Memory<byte>` is storable but implies async-friendly semantics that conflict with Conjecture's synchronous draw model. `ReadOnlySpan<byte>` is more precise about the read-only, synchronous intent.

**Store the span directly in `FromBytesStrategy<T>`.** Not possible: spans are ref structs and cannot be stored as fields. The `byte[]` copy is the correct implementation pattern.
