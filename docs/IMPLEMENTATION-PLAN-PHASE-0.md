# Phase 0 Implementation Plan: Conjecture.NET Core Engine

## Context

Conjecture.NET is a .NET port of Python's Hypothesis property-based testing library. The project scaffold is complete (empty `Conjecture.Core`, placeholder `Conjecture.Tests`) with 25 ADRs documenting all major design decisions. Phase 0 delivers a working end-to-end property test: random generation, basic strategies, LINQ combinators, a minimal `[Property]` attribute, and basic shrinking.

**End-state goal:** A user can write:
```csharp
[Property]
public void Addition_is_commutative(int x, int y) =>
    Assert.Equal(x + y, y + x);
```

## Dependency Graph

```
SplittableRandom -> PrngAdapter -> ConjectureData -> Strategy<T> -> Combinators
                                        |                               |
                                        v                               v
                                   TestRunner -----> PropertyAttribute
                                        |
                                        v
                                    Shrinker (ZeroBlocks, DeleteBlocks, LexMin, IntReduce)
```

## Pre-requisites

- [x] `/scaffold module Conjecture.Core Internal` -- Create Internal directory structure
- [x] `/scaffold module Conjecture.Core Strategies` -- Create Strategies directory structure

## TDD Execution Plan

Each cycle: `/test` (Red) then `/implement` (Green). 6 sub-phases, 18 cycles.

---

### 0.1 Random Source & Primitives

#### Cycle 0.1.1 -- IRandom + SplittableRandom
- [x] `/test` -- `src/Conjecture.Tests/Internal/SplittableRandomTests.cs`
  - Deterministic output from seed, different seeds diverge, NextBytes fills buffer, Split produces independent streams
- [x] `/implement` -- `src/Conjecture.Core/Internal/SplittableRandom.cs`
  - `interface IRandom` (`NextBytes(Span<byte>)`, `NextUInt64()`, `Split()`)
  - `sealed class SplittableRandom : IRandom` (SplitMix64-based)

#### Cycle 0.1.2 -- PrngAdapter (bounded integers)
- [x] `/test` -- `src/Conjecture.Tests/Internal/PrngAdapterTests.cs`
  - Bounded ulong in range, uniform distribution (chi-squared), zero-range returns bound
- [x] `/implement` -- `src/Conjecture.Core/Internal/PrngAdapter.cs`
  - Static rejection-sampling helpers for bounded integers from raw bytes

---

### 0.2 ConjectureData (The Heart)

#### Cycle 0.2.1 -- IRNode and data types
- [x] `/test` -- `src/Conjecture.Tests/Internal/IRNodeTests.cs`
  - IRNodeKind enum values, round-trip storage, integer constraints
- [x] `/implement` -- `src/Conjecture.Core/Internal/IRNodeKind.cs` (enum), `src/Conjecture.Core/Internal/IRNode.cs` (readonly struct)

#### Cycle 0.2.2 -- ConjectureData core draws
- [x] `/test` -- `src/Conjecture.Tests/Internal/ConjectureDataTests.cs`
  - DrawInteger in range, DrawBoolean both values, DrawBytes length, Status transitions, Freeze prevents draws, node recording order
- [x] `/implement` -- `src/Conjecture.Core/Internal/ConjectureData.cs`
  - `enum Status { Valid, Invalid, Interesting, Overrun }`
  - DrawInteger, DrawBoolean, DrawBytes, MarkInvalid, MarkInteresting, Freeze
  - ArrayPool<byte> backed buffer (ADR-0009)

#### Cycle 0.2.3 -- ConjectureData replay
- [x] `/test` -- `src/Conjecture.Tests/Internal/ConjectureDataReplayTests.cs`
  - Replay produces same nodes, overrun on short buffer, ForRecord factory
- [x] `/implement` -- Add `ConjectureData.ForRecord(IReadOnlyList<IRNode>)` static factory

---

### 0.3 Strategy<T> Base & Built-in Strategies

#### Cycle 0.3.1 -- Strategy<T> base
- [x] `/test` -- `src/Conjecture.Tests/Strategies/StrategyBaseTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/Strategy.cs` -- `abstract class Strategy<T>` with `abstract T Next(ConjectureData)`

#### Cycle 0.3.2 -- BooleanStrategy
- [x] `/test` -- `src/Conjecture.Tests/Strategies/BooleanStrategyTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/BooleanStrategy.cs` + `src/Conjecture.Core/Gen.cs` (`Gen.Booleans()`)

#### Cycle 0.3.3 -- IntegerStrategy (INumber<T>)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/IntegerStrategyTests.cs`
  - Default range, bounded, min==max, negative, long, byte via IBinaryInteger<T>
- [x] `/implement` -- `src/Conjecture.Core/Strategies/IntegerStrategy.cs`
  - `class IntegerStrategy<T> : Strategy<T> where T : IBinaryInteger<T>`
  - `Gen.Integers<T>(T? min, T? max)` + convenience overloads

#### Cycle 0.3.4 -- BytesStrategy
- [x] `/test` -- `src/Conjecture.Tests/Strategies/BytesStrategyTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/BytesStrategy.cs` + `Gen.Bytes(int size)`

---

### 0.4 LINQ Combinators

#### Cycle 0.4.1 -- Select (map)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/CombinatorTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/SelectStrategy.cs` + `src/Conjecture.Core/Strategies/StrategyExtensions.cs`

#### Cycle 0.4.2 -- Where (filter)
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/WhereStrategyTests.cs`
  - Filters output, exhausted budget marks invalid
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/WhereStrategy.cs` + `src/Conjecture.Core/UnsatisfiedAssumptionException.cs`

#### Cycle 0.4.3 -- SelectMany (bind)
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/SelectManyStrategyTests.cs`
  - Dependent generation, C# query syntax works
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/SelectManyStrategy.cs` (two-arg for query syntax)

#### Cycle 0.4.4 -- Zip
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/ZipStrategyTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/ZipStrategy.cs`

#### Cycle 0.4.5 -- Compose (imperative, ADR-0019)
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/ComposeTests.cs`
  - Imperative generation, dependent draws, Assume rejects
- [ ] `/implement` -- `src/Conjecture.Core/IGeneratorContext.cs` + `src/Conjecture.Core/Strategies/ComposeStrategy.cs` + `src/Conjecture.Core/Strategies/Strategies.cs`

---

### 0.5 Minimal Test Runner Integration

#### Cycle 0.5.1 -- ConjectureSettings
- [ ] `/test` -- `src/Conjecture.Tests/ConjectureSettingsTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/ConjectureSettings.cs` -- `record ConjectureSettings` (MaxExamples=100, Seed, etc.)

#### Cycle 0.5.2 -- TestRunner (engine loop)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/TestRunnerTests.cs`
  - Runs MaxExamples times, stops on failure, tracks unsatisfied, deterministic with seed
- [ ] `/implement` -- `src/Conjecture.Core/Internal/TestRunner.cs`

#### Cycle 0.5.3 -- PropertyAttribute (xUnit integration)
- [ ] `/test` -- `src/Conjecture.Tests/PropertyAttributeTests.cs`
  - Simple test passes, failing reports counterexample, bool params, seed determinism
- [ ] `/implement` -- `src/Conjecture.Core/PropertyAttribute.cs` + `Internal/ParameterStrategyResolver.cs` + `Internal/PropertyTestInvoker.cs`

#### Cycle 0.5.4 -- Assume.That
- [ ] `/test` -- `src/Conjecture.Tests/AssumeTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Assume.cs`

---

### 0.6 Basic Shrinking

#### Cycle 0.6.1 -- Shrinker infrastructure
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/ShrinkerTests.cs`
  - Trivial case unchanged, reduces buffer, preserves failure
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/Shrinker.cs` + `IShrinkPass.cs` + `ShrinkState.cs`

#### Cycle 0.6.2 -- ZeroBlocks pass (priority 0)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/ZeroBlocksPassTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/ZeroBlocksPass.cs`

#### Cycle 0.6.3 -- DeleteBlocks pass (priority 0)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/DeleteBlocksPassTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/DeleteBlocksPass.cs`

#### Cycle 0.6.4 -- LexicographicMinimize pass (priority 1)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/LexMinimizePassTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/LexMinimizePass.cs`

#### Cycle 0.6.5 -- IntegerReduction pass (priority 1)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/IntegerReductionPassTests.cs`
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/IntegerReductionPass.cs`

#### Cycle 0.6.6 -- Wire shrinker into TestRunner
- [ ] `/test` -- `src/Conjecture.Tests/Internal/TestRunnerShrinkingTests.cs`
  - Shrinking reports minimal counterexample, preserves failure, no shrink when passing
- [ ] `/implement` -- Modify TestRunner + PropertyAttribute to invoke Shrinker on Interesting status

---

## Post-implementation

- [ ] `/benchmark` -- Baseline perf numbers for ConjectureData + strategies
- [ ] Full verification: `dotnet test src/`

## Key Constraints

- **ArrayPool<byte> + Span<T>** for buffers (ADR-0009)
- **Nullable enabled, warnings as errors** -- null annotations on all public API
- **Internal by default** -- only `Gen`, `Strategy<T>`, `Assume`, `ConjectureSettings`, `PropertyAttribute`, `IGeneratorContext`, `Strategies` are public
- **IBinaryInteger<T>** for IntegerStrategy (ADR-0011)
- **File-scoped namespaces** throughout
- **No speculative optimization** (ADR-0025)
- Use `/decision` if design questions arise not covered by existing ADRs

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

End-to-end after 0.6:
```bash
dotnet test src/ --filter "FullyQualifiedName~PropertyAttributeTests"
```
Verify: `[Property]` test with `int` params runs 100 examples, a deliberately failing test shrinks to minimal counterexample.
