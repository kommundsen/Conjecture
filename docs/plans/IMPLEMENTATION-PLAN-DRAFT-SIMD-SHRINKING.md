# Draft: SIMD-Accelerated Shrinking

## Motivation

Shrinking is the most CPU-intensive phase of property testing. Conjecture's shrinker operates on byte buffers, making it a natural fit for SIMD vectorization. .NET 10 brings AVX10.2 support, improved JIT inlining, array interface devirtualization, and better struct argument handling — all directly benefiting hot shrink paths.

## .NET Advantage

.NET 10 provides `Vector<T>`, AVX10.2 intrinsics, and a JIT that auto-vectorizes common patterns. Because Conjecture's shrinker operates on contiguous byte buffers, these SIMD primitives map directly to shrink operations — zero-block detection, lexicographic comparison, and block equality can all be expressed as vectorized operations over `Span<byte>`.

## Key Ideas

### Vectorized Zero-Block Detection (Tier 0)
The `ZeroBlockPass` checks if byte ranges are already zero. Currently iterates byte-by-byte.
```csharp
// Current: sequential
bool IsZero(ReadOnlySpan<byte> block) { foreach (var b in block) if (b != 0) return false; return true; }

// Proposed: SIMD
bool IsZero(ReadOnlySpan<byte> block) => Vector.EqualsAll(new Vector<byte>(block), Vector<byte>.Zero);
```

### Vectorized Lexicographic Minimize (Tier 1)
`LexicographicMinimizePass` compares and reduces byte sequences. SIMD comparison can find the first differing byte in a single instruction.

### Vectorized Block Comparison (Tier 1)
`BlockSwappingPass` compares adjacent blocks. Vectorized equality check + early-exit.

### Profile-Guided Optimization
- Benchmark existing shrink passes to identify true hotspots
- Only apply SIMD where profiling shows >10% improvement
- Maintain scalar fallback for small buffers (< Vector<byte>.Count bytes)

## Design Decisions to Make

1. Use `Vector<T>` (portable) or `Avx2`/`Avx10v2` intrinsics (platform-specific)?
   - `Vector<T>` is recommended — JIT maps to best available ISA automatically
2. Minimum buffer size threshold for SIMD path vs scalar fallback?
3. How to handle buffer sizes not aligned to vector width?
4. Should SIMD paths be behind a `ConjectureSettings` flag for debugging?
5. Benchmark methodology: use existing `Conjecture.Benchmarks` project, add shrink-specific benchmarks

## Scope Estimate

Medium. Core vectorization is straightforward; ensuring correctness across all platforms and buffer alignments requires care. ~2 cycles.

## Dependencies

- `System.Runtime.Intrinsics` (in-box with .NET 10)
- `System.Numerics.Vector<T>` (in-box)
- Existing `Shrinker.cs` and `IShrinkPass` implementations
- `Conjecture.Benchmarks` project for regression gating

## Open Questions

- What is the actual distribution of buffer sizes during typical property tests? (Need profiling data to know if SIMD width matters)
- Does the JIT already auto-vectorize any of these loops in .NET 10? (May already be getting some benefit for free)
- How much does shrinking speed matter to end users vs generation speed? (Shrinking only runs on failure)
- Should we also SIMD-accelerate the `ConjectureData` buffer operations (read/write)?
