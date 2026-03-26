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

Each cycle: `/test` (Red) then `/implement` (Green). 8 sub-phases.

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
- [x] `/test` -- `src/Conjecture.Tests/Strategies/WhereStrategyTests.cs`
  - Filters output, exhausted budget marks invalid
- [x] `/implement` -- `src/Conjecture.Core/Strategies/WhereStrategy.cs` + `src/Conjecture.Core/UnsatisfiedAssumptionException.cs`

#### Cycle 0.4.3 -- SelectMany (bind)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/SelectManyStrategyTests.cs`
  - Dependent generation, C# query syntax works
- [x] `/implement` -- `src/Conjecture.Core/Strategies/SelectManyStrategy.cs` (two-arg for query syntax)

#### Cycle 0.4.4 -- Zip
- [x] `/test` -- `src/Conjecture.Tests/Strategies/ZipStrategyTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/ZipStrategy.cs`

#### Cycle 0.4.5 -- Compose (imperative, ADR-0019)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/ComposeTests.cs`
  - Imperative generation, dependent draws, Assume rejects
- [x] `/implement` -- `src/Conjecture.Core/IGeneratorContext.cs` + `src/Conjecture.Core/Strategies/ComposeStrategy.cs` + `src/Conjecture.Core/Strategies/Strategies.cs`

---

### 0.5 Minimal Test Runner Integration

#### Cycle 0.5.1 -- ConjectureSettings
- [x] `/test` -- `src/Conjecture.Tests/ConjectureSettingsTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/ConjectureSettings.cs` -- `record ConjectureSettings` (MaxExamples=100, Seed, etc.)

#### Cycle 0.5.2 -- TestRunner (engine loop)
- [x] `/test` -- `src/Conjecture.Tests/Internal/TestRunnerTests.cs`
  - Runs MaxExamples times, stops on failure, tracks unsatisfied, deterministic with seed
- [x] `/implement` -- `src/Conjecture.Core/Internal/TestRunner.cs`

#### Cycle 0.5.3 -- PropertyAttribute (xUnit integration)
- [x] `/test` -- `src/Conjecture.Tests/PropertyAttributeTests.cs`
  - Simple test passes, failing reports counterexample, bool params, seed determinism
- [x] `/implement` -- `src/Conjecture.Core/PropertyAttribute.cs` + `Internal/ParameterStrategyResolver.cs` + `Internal/PropertyTestCaseRunner.cs`

#### Cycle 0.5.4 -- Assume.That
- [x] `/test` -- `src/Conjecture.Tests/AssumeTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Assume.cs`

---

### 0.6 Basic Shrinking

#### Cycle 0.6.1 -- Shrinker infrastructure
- [x] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/ShrinkerTests.cs`
  - Trivial case unchanged, reduces buffer, preserves failure
- [x] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/Shrinker.cs` + `IShrinkPass.cs` + `ShrinkState.cs`

#### Cycle 0.6.2 -- ZeroBlocks pass (priority 0)
- [x] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/ZeroBlocksPassTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/ZeroBlocksPass.cs`

#### Cycle 0.6.3 -- DeleteBlocks pass (priority 0)
- [x] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/DeleteBlocksPassTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/DeleteBlocksPass.cs`

#### Cycle 0.6.4 -- LexicographicMinimize pass (priority 1)
- [x] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/LexMinimizePassTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/LexMinimizePass.cs`

#### Cycle 0.6.5 -- IntegerReduction pass (priority 1)
- [x] `/test` -- `src/Conjecture.Tests/Internal/Shrinker/IntegerReductionPassTests.cs`
- [x] `/implement` -- `src/Conjecture.Core/Internal/Shrinker/IntegerReductionPass.cs`

#### Cycle 0.6.6 -- Wire shrinker into TestRunner
- [x] `/test` -- `src/Conjecture.Tests/Internal/TestRunnerShrinkingTests.cs`
  - Shrinking reports minimal counterexample, preserves failure, no shrink when passing
- [x] `/implement` -- Modify TestRunner + PropertyAttribute to invoke Shrinker on Interesting status

---

### 0.7 Failure Reporting (ADR-0022, Phase 0 scope)

Phase 0 scope: format built-in primitive types (int, bool, byte[]) and report the seed. Full `IStrategyFormatter<T>` / `FormatterRegistry` deferred to Phase 1.

#### Cycle 0.7.1 -- CounterexampleFormatter
- [x] `/test` -- `src/Conjecture.Tests/Internal/CounterexampleFormatterTests.cs`
  - Formats int param as `x = 6`
  - Formats multiple params as `x = 6, y = -3` (one per line)
  - Formats bool param as `flag = True`
  - Includes seed in output: `Reproduce with: [Property(Seed = 0xDEADBEEF)]`
  - Falls back to `ToString()` for unknown types
- [x] `/implement` -- `src/Conjecture.Core/Internal/CounterexampleFormatter.cs`

#### Cycle 0.7.2 -- Wire formatter into PropertyTestCaseRunner
- [x] `/test` -- `src/Conjecture.Tests/PropertyAttributeFailureMessageTests.cs`
  - Failing `[Property]` test failure message contains param names and shrunk values
  - Failure message contains seed for reproduction
  - Passing test produces no failure message
- [x] `/implement` -- Modify `PropertyTestCaseRunner` to use `CounterexampleFormatter`
  - `TestRunResult` extended to carry seed + parameter metadata

---

### 0.8 End-to-End Self Tests

#### Cycle 0.8.1 -- Property attribute with shrinking end-to-end
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/PropertyShrinkingE2ETests.cs`
  - `[Property]` test with int params that fails produces shrunk minimal counterexample in failure message
  - `[Property]` test with multiple params shrinks each independently
  - `[Property]` test that always passes runs full MaxExamples without error
  - `[Property]` test using `Assume.That` discards invalid inputs and still shrinks on failure

#### Cycle 0.8.2 -- Combinator integration end-to-end
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/CombinatorE2ETests.cs`
  - `Select` (map) strategy generates transformed values end-to-end via TestRunner
  - `Where` (filter) strategy rejects invalid values and shrinks result
  - `SelectMany` (bind) with dependent generation shrinks both stages
  - `Compose` (imperative) with multiple draws shrinks to minimal failing input

#### Cycle 0.8.3 -- Shrink quality smoke tests
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/ShrinkQualityTests.cs`
  - Integer >= threshold shrinks to exactly threshold (not threshold+1, not 0)
  - Two integers whose sum exceeds threshold shrinks to minimal sum
  - Boolean param that must be true shrinks to true (not false)
  - Large bounded integer shrinks to smallest failing value within bounds

---

## Post-implementation

- [x] `/benchmark` -- Baseline perf numbers for ConjectureData + strategies
- [x] Full verification: `dotnet test src/`

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
