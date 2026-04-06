# Draft: Span-First Public API

## Motivation

C# 14 introduces first-class implicit conversions for `Span<T>` and `ReadOnlySpan<T>`. Arrays, strings, and other contiguous memory types implicitly convert to spans without explicit casting. This enables Conjecture's public API to accept `ReadOnlySpan<T>` parameters while users pass arrays naturally — achieving zero-copy semantics transparently.

## .NET Advantage

C# 14's first-class implicit conversions between `T[]`, `Span<T>`, and `ReadOnlySpan<T>` mean users can pass arrays where span parameters are expected — no explicit casting needed. This lets Conjecture expose zero-copy APIs for seed replay, buffer manipulation, and custom strategy authoring while keeping the call-site syntax simple and familiar.

## Key Ideas

### API Surface Changes
```csharp
// Seed replay — currently requires byte[]
ConjectureSettings settings = new() { Seed = 42 };
// Proposed: accept ReadOnlySpan<byte> for direct buffer replay
ConjectureSettings settings = new() { ReplayBuffer = stackalloc byte[] { ... } };

// Strategy from bytes — zero-copy
Strategy<T> Generate.FromBytes<T>(ReadOnlySpan<byte> buffer); // implicit from byte[]

// Custom strategy authoring
public abstract class Strategy<T>
{
    // New overload accepting span
    public T Generate(ReadOnlySpan<byte> randomness);
}
```

### Internal Optimizations
- `ConjectureData` already uses `ArrayPool<byte>` internally — expose span-based read/write
- Strategy `Compose<T>` context could offer `ReadOnlySpan<byte>` access to raw buffer
- Example database could load/store as spans without intermediate arrays

### C# 14 Implicit Conversions Enable
```csharp
byte[] seed = GetSeed();
Generate.FromBytes<int>(seed); // implicit byte[] → ReadOnlySpan<byte>

string text = "hello";
ReadOnlySpan<char> span = text; // implicit string → ReadOnlySpan<char>
```

## Design Decisions to Make

1. Which public APIs benefit most from span overloads? (Prioritize by usage frequency)
2. `ReadOnlySpan<T>` can't be stored in fields or used in async — how to handle in `ConjectureSettings` (a record)?
3. Should `IGeneratorContext.Generate()` expose a span-based overload for power users?
4. Breaking change risk: adding span overloads may cause overload resolution ambiguity with existing byte[] overloads
5. How to document the span pattern for custom strategy authors?

## Scope Estimate

Small-Medium. API design + overload additions. No algorithmic changes. ~1-2 cycles.

## Dependencies

- C# 14 compiler (implicit span conversions)
- Existing `ConjectureData` buffer management
- Public API compatibility tracking (`PublicAPI.Unshipped.txt`)

## Open Questions

- Are there real use cases for span-based seed replay, or is the `ulong Seed` property sufficient?
- Do span overloads complicate the source generator output?
- Should we provide `Memory<byte>`-based alternatives for async scenarios?
