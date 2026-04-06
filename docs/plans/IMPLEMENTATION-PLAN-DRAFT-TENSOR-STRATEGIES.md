# Draft: Tensor Strategies

## Motivation

`System.Numerics.Tensors` is stable and non-experimental in .NET 10. Tensor<T> provides n-dimensional array semantics with SIMD-accelerated operations, slicing, reshaping, and arithmetic via generic math interfaces. ML/AI workloads increasingly dominate .NET development, yet no property-based testing library offers tensor generation or shrinking.

## .NET Advantage

`System.Numerics.Tensors` is now a stable, in-box .NET 10 API with first-class generic math integration. Because `Tensor<T>` implements `ITensor<TSelf, T>` and supports SIMD-accelerated operations natively, Conjecture can offer deep integration — shape-aware generation, element-level shrinking, and type-safe strategies — all backed by the same generic math interfaces the numeric strategies already use.

## Key Ideas

- `Generate.Tensors<T>(ReadOnlySpan<nint> shape)` — generate tensors with a fixed shape
- `Generate.Tensors<T>(Strategy<nint[]> shapeStrategy)` — generate tensors with variable shapes
- Element generation delegates to existing `Generate.Integers<T>()`, `Generate.Floats()`, etc.
- Tensor-aware shrinking:
  - Reduce shape dimensions (remove trailing dimensions)
  - Shrink individual element values via existing numeric shrink passes
  - Shrink toward zero-tensors
- `Generate.SparseTensors<T>(shape, density)` — tensors with configurable sparsity
- Integration with `ITensor<TSelf, T>` and `IReadOnlyTensor<TSelf, T>` interfaces

## Design Decisions to Make

1. Should shape strategies be bounded by default? (e.g., max rank 4, max dimension size 64)
2. How to handle non-numeric tensor element types (e.g., `Tensor<bool>`)?
3. Should we support generating tensors that satisfy constraints (e.g., positive-definite, symmetric)?
4. How does tensor shrinking interact with the byte-buffer IR? Tensors can be large — need to avoid blowing up the buffer.
5. NuGet dependency: `System.Numerics.Tensors` is a separate package — ship as `Conjecture.Tensors` or bundle in Core?

## Scope Estimate

Medium. Core strategy + shrinking support is ~2-3 cycles. Constrained tensor generation (symmetric, PD) is a stretch goal.

## Dependencies

- `System.Numerics.Tensors` NuGet package (v10.0+)
- Existing `IntegerStrategy<T>`, `FloatingPointStrategy<T>` for element generation
- Shrinker infrastructure for buffer-level shrinking

## Open Questions

- What tensor shapes are most useful for testing? Survey ML frameworks (TorchSharp, ML.NET, ONNX Runtime).
- Should we provide strategies for common ML data structures beyond raw tensors (e.g., batched inputs, feature vectors)?
- Performance budget: how large a tensor can we generate and shrink within a reasonable test timeout?
