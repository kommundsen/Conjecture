# Phase 2 Implementation Plan: Advanced Shrinking, xUnit Polish & Reporting

## Context

Phase 0 delivered a working Conjecture engine with 4 basic shrinker passes (zero blocks, delete blocks, lex minimize, integer reduction), basic strategies, LINQ combinators, and a minimal `[Property]` attribute. Phase 1 extended the library with rich strategies (floats, strings, collections, choice), full formatter pipeline, extended settings, and SQLite example database. Phase 2 makes the library production-quality: the shrinker becomes best-in-class with 6 advanced passes (ADR-0021), xUnit integration gains `[Example]`, `[From<T>]`, `[FromFactory]`, and async support, failure reporting shows original vs shrunk counterexamples with trimmed stack traces, and trim/NativeAOT compatibility is validated.

**Deferred to Phase 3+:** Source generators (ADR-0010), Roslyn analyzers (ADR-0023), stateful testing (ADR-0015), F# API (ADR-0013), NUnit/MSTest integration.

**End-state goal:** A user can write:
```csharp
public sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Gen.Integers(min: 1);
}

[Property]
[Example(1, 1)]
[Example(int.MaxValue, 1)]
public void Multiplication_is_commutative(
    [From<PositiveInts>] int a,
    [From<PositiveInts>] int b) =>
    Assert.Equal(a * b, b * a);

[Property]
public async Task Database_round_trips(string key) {
    await db.SetAsync(key, key);
    Assert.Equal(key, await db.GetAsync(key));
}
```
and get a failure message like:
```
Falsifying example found after 23 examples (shrunk 14 times):
  a = 1
  b = 1
Reproduce with: [Property(Seed = 0xA7F3B2E1)]
```
with advanced shrinking that reduces floats, strings, and collections to minimal counterexamples.

## Dependency Graph

```
IRNodeKind extension (Float64/Float32/StringChar/StringLength)
       │
       ├──> FloatSimplificationPass ──┐
       ├──> StringAwarePass ──────────┤
       │                              │
IntervalDeletionPass ─────────────────┤
BlockSwappingPass ────────────────────┤
RedistributionPass ───────────────────┤
AdaptivePass ─────────────────────────┤
                                      v
                              Shrinker (wired)
                                      │
IStrategyProvider<T> ─────────────────┤
FromAttribute<T> ──> ParameterStrategyResolver (extended)
FromFactoryAttribute ──────────┘      │
ExampleAttribute ──────────────────> PropertyTestCaseRunner (modified)
Async support ─────────────────────┘  │
                                      v
Enhanced CounterexampleFormatter ──> Failure output
                                      │
Trimming annotations ─────────────> CI validation
```

## Pre-requisites

- [ ] `/decision` -- ADR-0028: Parameter Strategy Resolution Attributes — design for `IStrategyProvider<T>`, `[From<T>]`, `[FromFactory]`, `[Example]`
- [x] Extend `IRNodeKind` with `Float64`, `Float32`, `StringChar`, `StringLength` variants
- [x] Update `FloatingPointStrategy<T>` to record `Float64`/`Float32` kind nodes
- [x] Update `StringStrategy` to record `StringLength`/`StringChar` kind nodes
- [x] Update `TestRunner.SerializeNodes`/`DeserializeNodes` for new `IRNodeKind` values
- [ ] Update `PublicAPI.Unshipped.txt` for every cycle adding public API (ADR-0002)

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red → Green → Refactor → Verify → Mark done). 11 sub-phases.

---

### 2.1 Shrinker Infrastructure: IR Node Kind Metadata

Extend `IRNodeKind` so specialized shrinker passes can identify float and string nodes. This is invisible to users but enables Passes 2.4.

#### Cycle 2.1.1 -- IRNodeKind extension + strategy updates
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/IRNodeKindExtendedTests.cs`
    - `IRNodeKind.Float64` and `Float32` exist, `StringChar` and `StringLength` exist
    - `IRNode.ForFloat64(value, min, max)` and `ForFloat32` factory methods work
    - `IRNode.ForStringLength(value, min, max)` and `ForStringChar(value, min, max)` work
    - Existing Integer/Boolean/Bytes kinds unchanged
  - **Impl** -- Modify `src/Conjecture.Core/Internal/IRNodeKind.cs` + `src/Conjecture.Core/Internal/IRNode.cs`
    - Add enum values: `Float64 = 3, Float32 = 4, StringLength = 5, StringChar = 6`
    - Add factory methods that delegate to constructor with new kinds

#### Cycle 2.1.2 -- Wire new kinds into strategies + serialization
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/IRNodeKindWiringTests.cs`
    - `FloatingPointStrategy<double>` records `Float64` kind nodes (not `Integer`)
    - `FloatingPointStrategy<float>` records `Float32` kind nodes
    - `StringStrategy` records `StringLength` for length node and `StringChar` for character nodes
    - Serialization round-trips new kinds through `TestRunner` serialize/deserialize
    - Old serialized data (Integer kind) still deserializes correctly (backward compat)
  - **Impl** -- Modify `ConjectureData` to add `DrawFloat64`, `DrawFloat32`, `DrawStringLength`, `DrawStringChar` methods that record correct kind; update `FloatingPointStrategy<T>` and `StringStrategy` to use them; update `TestRunner.SerializeNodes`/`DeserializeNodes`

---

### 2.2 Advanced Shrinker: Structural Passes

#### Cycle 2.2.1 -- IntervalDeletionPass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/IntervalDeletionPassTests.cs`
    - Deletes contiguous runs of 2+ nodes in one operation
    - Preserves failure status after deletion
    - Reduces node count more aggressively than single-node DeleteBlocks
    - No-op when all single deletions are invalid
    - Works with varying interval sizes (2, 4, 8)
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/IntervalDeletionPass.cs`
    - Try deleting intervals of decreasing size (8, 4, 2) at each position
    - Call `state.TryUpdate` with candidate minus the interval

#### Cycle 2.2.2 -- BlockSwappingPass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/BlockSwappingPassTests.cs`
    - Swaps adjacent same-kind nodes to find lexicographically smaller arrangement
    - `[5, 3]` integer nodes → `[3, 5]` if still interesting
    - No-op when nodes are already in ascending order
    - Preserves failure status
    - Only swaps nodes of same kind and compatible ranges
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/BlockSwappingPass.cs`
    - Iterate pairs; if `nodes[i].Value > nodes[i+1].Value` and same kind, try swap

---

### 2.3 Advanced Shrinker: Value-Level Passes

#### Cycle 2.3.1 -- RedistributionPass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/RedistributionPassTests.cs`
    - Moves magnitude between adjacent integer nodes: `(5, 3)` → tries `(4, 4)`, `(3, 5)`, `(2, 6)`, etc.
    - Finds lexicographically smallest pair that preserves failure
    - No-op when nodes are at minimum already
    - Works across integer nodes only (skips Boolean, Bytes)
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/RedistributionPass.cs`
    - For each adjacent integer pair, try shifting value from right to left (decreasing left, increasing right) while staying in bounds

#### Cycle 2.3.2 -- AdaptivePass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/AdaptivePassTests.cs`
    - Tracks which node indices produced progress on prior passes
    - Biases effort toward recently productive indices
    - Falls back to full scan when adaptive set is empty
    - Improves shrink efficiency (fewer total attempts for same result)
    - Integrates with ShrinkState to read progress history
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/AdaptivePass.cs`
    - Wrap existing passes; maintain `HashSet<int>` of productive indices; retry those first
    - Extend `ShrinkState` with `LastModifiedIndex` tracking

---

### 2.4 Advanced Shrinker: Specialized Passes

#### Cycle 2.4.1 -- FloatSimplificationPass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/FloatSimplificationPassTests.cs`
    - NaN bit pattern → tries 0.0 bit pattern
    - ±Infinity → tries max/min finite value, then 0.0
    - Large float → tries smaller float toward 0.0
    - Negative float → tries positive equivalent
    - Only operates on `Float64`/`Float32` kind nodes
    - Skips `Integer` kind nodes
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/FloatSimplificationPass.cs`
    - Identify Float64/Float32 nodes by kind
    - Try replacements: NaN→0, ±Inf→max_finite→0, large→small via binary search on float magnitude

#### Cycle 2.4.2 -- StringAwarePass
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/StringAwarePassTests.cs`
    - Reduces string length node AND deletes corresponding character nodes atomically
    - Simplifies character nodes toward 'a' (codepoint 97) or space (32)
    - Preserves failure after simplification
    - Only operates on `StringLength`/`StringChar` kind nodes
    - Correctly handles multi-character deletions
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker/StringAwarePass.cs`
    - Find StringLength node, try reducing it while deleting trailing StringChar nodes
    - For each StringChar node, try replacing value with 'a', then space

---

### 2.5 Wire Passes & Shrink Quality

#### Cycle 2.5.1 -- Wire new passes into Shrinker with priority tiers
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/ShrinkerPassOrderTests.cs`
    - All 10 passes are registered
    - Cheap passes (tier 0: zero, delete, interval-delete) run before expensive (tier 2: adaptive, float, string)
    - Each tier runs to fixpoint before next tier
    - Full shrink loop converges
  - **Impl** -- Modify `src/Conjecture.Core/Internal/Shrinker/Shrinker.cs`
    - Group passes by priority tier; loop each tier to fixpoint
    - Tier 0: ZeroBlocks, DeleteBlocks, IntervalDeletion
    - Tier 1: LexMinimize, IntegerReduction, BlockSwapping, Redistribution
    - Tier 2: FloatSimplification, StringAware, Adaptive

#### Cycle 2.5.2 -- Shrink quality regression tests
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/Shrinker/ShrinkQualityAdvancedTests.cs`
    - Float > threshold shrinks to exactly threshold (not NaN, not huge number)
    - String containing "error" shrinks to shortest string still containing "error"
    - List of ints with sum > 100 shrinks to minimal list (e.g. `[101]` or `[51, 50]`)
    - Two adjacent integers where a > b shrinks to minimal pair (e.g. `1, 0`)
    - Nested collection shrinks both outer and inner dimensions

---

### 2.6 [Example] Attribute

#### Cycle 2.6.1 -- ExampleAttribute
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/ExampleAttributeTests.cs`
    - `[Example(1, 2)]` stores arguments, `[Example("hello")]` stores string arg
    - Multiple `[Example]` on same method all recorded
    - `Arguments` property returns the constructor args
    - Works with int, string, bool, null, enum, typeof() args (attribute-legal types)
  - **Impl** -- `src/Conjecture.Core/ExampleAttribute.cs`
    - `[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]`
    - `public sealed class ExampleAttribute(params object?[] arguments) : Attribute`
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 2.6.2 -- Wire [Example] into PropertyTestCaseRunner
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/PropertyAttributeExampleTests.cs`
    - `[Example(0, 0)]` case runs before generated examples
    - Multiple `[Example]` attributes all execute in order
    - Failing explicit example reports which example failed
    - Explicit examples contribute to `ExampleCount` in output
    - Generated examples still run after explicit ones
    - `[Example]` with wrong arg count throws clear error
  - **Impl** -- Modify `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs`
    - Extract `[Example]` attributes from test method
    - Run each before calling `TestRunner.Run`
    - On failure, report as explicit example (no shrinking needed)

---

### 2.7 [From\<T\>] and [FromFactory] Attributes

#### Cycle 2.7.1 -- IStrategyProvider\<T\> interface
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StrategyProviderTests.cs`
    - Custom `IStrategyProvider<int>` creates strategy, `Create()` returns working strategy
    - Strategy from provider generates values correctly via TestRunner
    - Provider with composed strategy (Gen.Integers().Where(...)) works
  - **Impl** -- `src/Conjecture.Core/IStrategyProvider.cs`
    - `public interface IStrategyProvider<out T> { Strategy<T> Create(); }`
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 2.7.2 -- FromAttribute\<T\> + resolver integration
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/FromAttributeTests.cs`
    - `[From<PositiveInts>]` on parameter recognized by resolver
    - Resolver instantiates provider and uses its strategy
    - Type mismatch (provider returns `Strategy<string>` for `int` param) throws clear error
    - Without `[From]`, falls back to type inference (existing behavior)
    - Works alongside unlabeled parameters
  - **Impl** -- `src/Conjecture.Core/FromAttribute.cs` + modify `src/Conjecture.Xunit/Internal/ParameterStrategyResolver.cs`
    - `[AttributeUsage(AttributeTargets.Parameter)]`
    - `public sealed class FromAttribute<TProvider> : Attribute where TProvider : IStrategyProvider, new()`
    - Note: generic constraint uses marker `IStrategyProvider` base interface; runtime check for `IStrategyProvider<T>` matching param type
    - Resolver: check parameter for `FromAttribute<>`, instantiate, cast to `IStrategyProvider<T>`, call `Create()`, draw from it
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 2.7.3 -- FromFactoryAttribute + resolver integration
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/FromFactoryAttributeTests.cs`
    - `[FromFactory(nameof(EvenInts))]` finds static method on test class
    - Method returning `Strategy<int>` works for `int` param
    - Missing method throws `InvalidOperationException` with available methods listed
    - Non-static method throws clear error
    - Wrong return type throws clear error
  - **Impl** -- `src/Conjecture.Core/FromFactoryAttribute.cs` + modify resolver
    - `[AttributeUsage(AttributeTargets.Parameter)]`
    - `public sealed class FromFactoryAttribute(string methodName) : Attribute`
    - Resolver: reflect on declaring type to find static method, invoke, draw from returned strategy
    - Update `PublicAPI.Unshipped.txt`

---

### 2.8 Async [Property] Support

#### Cycle 2.8.1 -- Async test method detection and execution
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/AsyncPropertyTests.cs`
    - `[Property]` method returning `Task` executes and passes
    - `[Property]` async method that throws fails with counterexample
    - `[Property]` method returning `ValueTask` works
    - Parameters are resolved identically to sync methods
    - Seed determinism preserved for async methods
  - **Impl** -- Modify `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs`
    - Detect async return type (`Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`)
    - If async: `await (Task)methodInfo.Invoke(testInstance, args)!`
    - If sync: existing path

#### Cycle 2.8.2 -- Async shrinking and deadline
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/AsyncPropertyShrinkingTests.cs`
    - Async failing test shrinks to minimal counterexample
    - `Deadline` setting applies per-example to async methods
    - Shrinking replays are also async-aware
    - Database round-trip works with async failures
  - **Impl** -- Modify `src/Conjecture.Core/Internal/TestRunner.cs`
    - Add `RunAsync` overload accepting `Func<ConjectureData, Task>` test delegate
    - Shrinker's `Replay` becomes async-capable
    - `PropertyTestCaseRunner` routes async methods through async path

---

### 2.9 Enhanced Failure Reporting

#### Cycle 2.9.1 -- Original vs shrunk counterexample + improved format
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/EnhancedReportingTests.cs`
    - When shrinkCount > 0: output shows both "Falsifying example" (original) and "Minimal counterexample" (shrunk)
    - When shrinkCount == 0: output shows only "Falsifying example"
    - Output includes "found after N examples (shrunk M times)"
    - Reproduce line always present
    - Values use FormatterRegistry
  - **Impl** -- Modify `src/Conjecture.Core/Internal/CounterexampleFormatter.cs`
    - Add overload accepting both original and shrunk nodes
    - Extend `TestRunResult` with `OriginalCounterexample` (pre-shrink nodes)
    - Modify `TestRunner` to capture original nodes before shrinking

#### Cycle 2.9.2 -- Stack trace trimming
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/StackTraceTrimmerTests.cs`
    - Frames from `Conjecture.Core.Internal` are removed
    - Frames from `Conjecture.Xunit.Internal` are removed
    - Frames from `System.RuntimeMethodHandle` and reflection internals are removed
    - User test method frames are preserved
    - xUnit runner frames are preserved (they're useful for IDE navigation)
    - Empty/null stack trace returns empty string
  - **Impl** -- `src/Conjecture.Core/Internal/StackTraceTrimmer.cs`
    - Filter `StackTrace.GetFrames()` by namespace prefix blacklist
    - Wire into `PropertyTestCaseRunner.BuildFailureMessage`

---

### 2.10 Trim/NativeAOT Validation

#### Cycle 2.10.1 -- Trimming annotations
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/TrimAnnotationTests.cs`
    - `[RequiresUnreferencedCode]` present on `ParameterStrategyResolver.Resolve`
    - `[RequiresDynamicCode]` present on `PropertyTestCaseRunner.RunTestAsync`
    - `[assembly: AssemblyMetadata("IsTrimmable", "True")]` in Conjecture.Core
    - No trim-unsafe patterns in public API paths (Gen.*, Strategy<T>, IStrategyProvider<T>, etc.)
  - **Impl** -- Add trimming annotations to `Conjecture.Core` and `Conjecture.Xunit`
    - Public API surface must be trim-safe
    - xUnit integration (internal, uses reflection) is annotated as trim-unsafe

#### Cycle 2.10.2 -- CI trim validation setup
- [ ] `/implement-cycle`
  - **Tests** -- Verify: `dotnet publish src/Conjecture.Core -c Release` with `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` produces zero warnings
  - **Impl** -- Add `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` and `<IsTrimmable>true</IsTrimmable>` to `Conjecture.Core.csproj`
    - Document trim validation in CI notes

---

### 2.11 End-to-End Tests

#### Cycle 2.11.1 -- Advanced shrinking end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/EndToEnd/AdvancedShrinkingE2ETests.cs`
    - Float property `x > 100.0` shrinks to exactly `100.0...01` (not NaN, not infinity)
    - String property `s.Contains("err")` shrinks to `"err"` (shortest matching)
    - List property `xs.Sum() > 100` shrinks to minimal list
    - Multi-param property shrinks each independently via redistribution
    - [Property] with Deadline still shrinks correctly

#### Cycle 2.11.2 -- [Example] + [From\<T\>] end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/EndToEnd/AttributeE2ETests.cs`
    - `[Property]` with `[Example(0, 0)]` runs explicit case first, then generates
    - `[Property]` with `[From<PositiveInts>]` generates only positive ints
    - `[Property]` with `[FromFactory(nameof(EvenInts))]` generates only even ints
    - Mixed: some params with `[From]`, others inferred
    - Failing `[From]`-constrained test shrinks within strategy bounds

#### Cycle 2.11.3 -- Async property end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/EndToEnd/AsyncPropertyE2ETests.cs`
    - Async `[Property]` returning `Task` passes when logic is correct
    - Async `[Property]` that throws shrinks and reports counterexample
    - Async with `[Example]` runs explicit case first
    - Async with `[From<T>]` generates from custom strategy

#### Cycle 2.11.4 -- Reporting quality end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/EndToEnd/ReportingQualityE2ETests.cs`
    - Failure output contains "Falsifying example" and "Minimal counterexample" sections
    - Failure output uses registered formatters (string in quotes, list in brackets)
    - Stack trace in output excludes Conjecture internals
    - Seed in output is valid for reproduction via `[Property(Seed = ...)]`

---

## Post-implementation

- [ ] `/benchmark` -- Perf: Shrinker total time for standard failing properties (compare Phase 1 baseline), individual pass contribution, async property overhead
- [ ] Full verification: `dotnet test src/`

## Key Constraints

- **`PublicAPI.Unshipped.txt`** updated every cycle that adds public API (ADR-0002, ADR-0003)
- **NativeAOT safe** -- all new public API (IStrategyProvider, FromAttribute, ExampleAttribute) trim-safe; xUnit internals annotated (ADR-0014)
- **ArrayPool\<byte\> + Span\<T\>** for buffers (ADR-0009)
- **Nullable enabled, warnings as errors** -- null annotations on all public API
- **Internal by default** -- new public types: `IStrategyProvider<T>`, `FromAttribute<T>`, `FromFactoryAttribute`, `ExampleAttribute`; all shrinker passes remain internal
- **File-scoped namespaces** throughout
- **.NET 10 minimum** -- C# 14 generic attributes available (ADR-0006)
- **No speculative optimization** -- profile before optimizing; BenchmarkDotNet baselines required (ADR-0025)
- **Shrinker pass priority tiers** -- cheap passes to fixpoint before expensive (ADR-0021)
- **Backward-compatible serialization** -- new IRNodeKind values must not break existing database entries
- Use `/decision` if design questions arise not covered by existing ADRs

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 2.5 (shrinker complete):
```bash
dotnet test src/ --filter "FullyQualifiedName~Shrinker"
dotnet test src/ --filter "FullyQualifiedName~ShrinkQuality"
```

After 2.7 (attributes complete):
```bash
dotnet test src/ --filter "FullyQualifiedName~FromAttribute"
dotnet test src/ --filter "FullyQualifiedName~Example"
```

After 2.8 (async):
```bash
dotnet test src/ --filter "FullyQualifiedName~Async"
```

End-to-end after 2.11:
```bash
dotnet test src/ --filter "FullyQualifiedName~EndToEnd"
```

Final:
```bash
dotnet test src/
dotnet build src/ -c Release
```

## New ADR(s) Needed

- **ADR-0028: Parameter Strategy Resolution Attributes** — documents design for `IStrategyProvider<T>`, `FromAttribute<T>`, `FromFactoryAttribute`, `ExampleAttribute`. Key decisions: `IStrategyProvider<T>` interface (not base class inheritance) for user-defined strategies, generic attribute constraint pattern, runtime type validation in resolver, attribute placement in `Conjecture.Core` (framework-agnostic).

## Critical Files

### Modified
- `src/Conjecture.Core/Internal/IRNodeKind.cs` — new enum values
- `src/Conjecture.Core/Internal/IRNode.cs` — new factory methods
- `src/Conjecture.Core/Internal/ConjectureData.cs` — new draw methods for float/string kinds
- `src/Conjecture.Core/Internal/Shrinker/Shrinker.cs` — tiered pass scheduling, 10 passes
- `src/Conjecture.Core/Internal/TestRunner.cs` — async overload, original counterexample capture
- `src/Conjecture.Core/Internal/TestRunResult.cs` — OriginalCounterexample field
- `src/Conjecture.Core/Internal/CounterexampleFormatter.cs` — original vs shrunk display
- `src/Conjecture.Core/Generation/FloatingPointStrategy.cs` — use Float64/Float32 draw
- `src/Conjecture.Core/Generation/StringStrategy.cs` — use StringLength/StringChar draw
- `src/Conjecture.Xunit/Internal/ParameterStrategyResolver.cs` — [From<T>]/[FromFactory] support
- `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs` — [Example], async, enhanced reporting

### New
- `src/Conjecture.Core/IStrategyProvider.cs`
- `src/Conjecture.Core/FromAttribute.cs`
- `src/Conjecture.Core/FromFactoryAttribute.cs`
- `src/Conjecture.Core/ExampleAttribute.cs`
- `src/Conjecture.Core/Internal/Shrinker/IntervalDeletionPass.cs`
- `src/Conjecture.Core/Internal/Shrinker/BlockSwappingPass.cs`
- `src/Conjecture.Core/Internal/Shrinker/RedistributionPass.cs`
- `src/Conjecture.Core/Internal/Shrinker/AdaptivePass.cs`
- `src/Conjecture.Core/Internal/Shrinker/FloatSimplificationPass.cs`
- `src/Conjecture.Core/Internal/Shrinker/StringAwarePass.cs`
- `src/Conjecture.Core/Internal/StackTraceTrimmer.cs`
