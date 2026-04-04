# Phase 5 Implementation Plan: Targeted Property Testing & Recursive Strategies

## Context

Phase 0 delivered the core Conjecture engine (random generation, basic strategies, LINQ combinators, `[Property]` attribute, basic shrinking). Phase 1 extended with rich strategies (floats, strings, collections, choice), formatter pipeline, settings system, and SQLite example database. Phase 2 made it production-quality: 10-pass shrinker (3 tiers), `[Example]`/`[From<T>]`/`[FromFactory]` attributes, async support, enhanced failure reporting, and trim/NativeAOT validation. Phase 3 broadened developer tooling: Roslyn source generator for automatic `Arbitrary<T>` derivation, 6 Roslyn analyzers, and xUnit v3/NUnit/MSTest framework adapters. Phase 4 delivered stateful testing: `IStateMachine<TState,TCommand>`, command sequence shrinking, `StateMachineFormatter`.

Phase 5 delivers two major capabilities deferred from earlier phases:

1. **Targeted property testing** (Python Hypothesis's `hypothesis.target()`) — coverage-guided generation that uses user-provided numeric observations to steer the engine toward interesting regions of the input space via hill climbing on the IR buffer.

2. **Recursive/tree-shaped strategies** (`Generate.Recursive<T>()`) — enabling generation of recursive data structures (expression trees, JSON, nested lists) with bounded depth control.

**Deferred to Phase 6+:** F# API (ADR-0013).

**End-state goal:** A user can write:

```csharp
[Property]
public void Sorting_preserves_length(List<int> xs)
{
    var sorted = xs.OrderBy(x => x).ToList();
    // Guide the engine toward longer lists
    Target.Maximize(xs.Count, "list_length");
    Assert.Equal(xs.Count, sorted.Count);
}
```

And the engine will, after an initial generation phase, enter a targeting phase where it hill-climbs on the IR buffer to maximize `xs.Count`, finding larger and more complex inputs that still satisfy the property.

For recursive strategies:

```csharp
Strategy<Expr> exprStrategy = Generate.Recursive<Expr>(
    baseCase: Generate.Integers<int>(0, 100).Select(n => (Expr)new Literal(n)),
    recursive: self => Generate.OneOf(
        Generate.Integers<int>(0, 100).Select(n => (Expr)new Literal(n)),
        self.Zip(self, (l, r) => (Expr)new Add(l, r)),
        self.Zip(self, (l, r) => (Expr)new Mul(l, r))
    ),
    maxDepth: 5);
```

## Dependency Graph

```
ADR-0035 (targeted testing API) ──────────────────────────────────┐
ADR-0036 (recursive strategy design) ─────────────────────────┐   │
                                                               │   │
5.1 Observations on ConjectureData ────────────────────────────┼───┘
         │                                                     │
         v                                                     │
5.2 Target static class + IGeneratorContext.Target ─────────────┤
         │                                                     │
         v                                                     │
5.3 HillClimber (internal) ────────────────────────────────────┤
         │                                                     │
         v                                                     │
5.4 Targeting phase in TestRunner ─────────────────────────────┤
         │                                                     │
         v                                                     │
5.5 Settings (Targeting, TargetingProportion) ─────────────────┤
         │                                                     │
         v                                                     │
5.6 Failure reporting (target scores in output) ───────────────┤
         │                                                     │
         v                                                     │
5.7 RecursiveStrategy ─────────────────────────────────────────┘
         │
         v
5.8 E2E targeting tests → 5.9 E2E recursive tests
         │
         v
5.10 SelfTests → 5.11 API surface → 5.12 Benchmarks → 5.13 Docs
```

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red → Green → Refactor → Verify → Mark done). 20 sub-phases.

---

### 5.0 Pre-requisites

#### Cycle 5.0.1 -- ADRs
- [ ] `/decision` -- ADR-0035: Targeted Testing API
  - Two exposure paths: (1) `Target.Maximize(double, string)` / `Target.Minimize(double, string)` static methods callable from `[Property]` test bodies (parallels `Assume.That` pattern); (2) `IGeneratorContext.Target(double, string)` callable inside `Generate.Compose` blocks
  - Both paths write observations into `ConjectureData.Observations` (`Dictionary<string, double>`)
  - `Target.Minimize(x, label)` is sugar for `Target.Maximize(-x, label)`
  - `TestRunner` gains a targeting phase after generation: for each label, take the best-scoring `(nodes, score)` pair and hill-climb by mutating individual IR node values
  - Hill climber mutates `IsIntegerLike` nodes via binary search toward better scores; also tries small random perturbations
  - `ConjectureSettings` gains `Targeting` (bool, default true) and `TargetingProportion` (double, default 0.5 — fraction of `MaxExamples` budget for targeting phase)
  - Multiple labels supported; targeting phase round-robins between labels
  - `AsyncLocal<ConjectureData>` ambient context avoids thread-local problems with async tests (ADR-0017)
  - Observations are purely advisory — they never affect shrinking behavior; shrinking still works on interestingness (exceptions)
- [ ] `/decision` -- ADR-0036: Recursive Strategy Design
  - `Generate.Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)` — user provides base case and a function that receives a "self" strategy and returns a combined strategy
  - `RecursiveStrategy<T>` internally tracks depth via IR integer draws from the stream; when `maxDepth` is reached, the "self" strategy substitutes `baseCase`
  - Depth tracking uses integer IR node draws that the shrinker can reduce to 0 (preferring shallower trees) via existing `IntegerReductionPass`
  - NativeAOT-safe: no reflection, just generic lambda composition

---

### 5.1 Observation Tracking

#### Cycle 5.1.1 -- ConjectureData.Observations
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/ConjectureDataTargetTests.cs`
    - `ConjectureData.Observations` is empty by default
    - `ConjectureData.RecordObservation("label", 42.0)` stores the value; second call with same label overwrites
    - `ConjectureData.RecordObservation` on frozen data throws `InvalidOperationException`
    - `ConjectureData.RecordObservation` with `NaN` throws `ArgumentException`
    - `ConjectureData.RecordObservation` with `+/-Infinity` throws `ArgumentException`
    - Multiple labels work independently
    - Observations readable after `Freeze()`
  - **Impl** -- `src/Conjecture.Core/Internal/ConjectureData.cs`
    - Add `private readonly Dictionary<string, double> observations = [];`
    - Add `internal IReadOnlyDictionary<string, double> Observations => observations;`
    - Add `internal void RecordObservation(string label, double value)` — validates not frozen, not NaN, not Infinity

---

### 5.2 Target Public API

#### Cycle 5.2.1 -- Target static class
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/TargetTests.cs`
    - `Target.Maximize(10.0)` within a test body (invoked via `TestRunner`) records observation with label `"default"`
    - `Target.Maximize(10.0, "custom")` records with label `"custom"`
    - `Target.Minimize(10.0, "custom")` records `-10.0` with label `"custom"`
    - `Target.Maximize` outside a test context throws `InvalidOperationException` with clear message
    - Multiple calls to `Target.Maximize` with same label: last value wins
    - `Target.Maximize(double.NaN)` throws `ArgumentException`
  - **Impl** -- `src/Conjecture.Core/Target.cs`
    - `public static class Target` with `Maximize(double, string label = "default")` and `Minimize(double, string label = "default")`
    - `internal static readonly AsyncLocal<ConjectureData?> CurrentData = new();`
    - `Maximize` delegates to `CurrentData.Value!.RecordObservation(label, value)`
    - `Minimize` delegates to `CurrentData.Value!.RecordObservation(label, -value)`
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 5.2.2 -- IGeneratorContext.Target integration
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Strategies/ComposeTargetTests.cs`
    - `Generate.Compose(ctx => { var n = ctx.Generate(Generate.Integers<int>()); ctx.Target(n, "size"); return n; })` records observation when used in `TestRunner`
    - `ctx.Target(value)` uses label `"default"`
    - `ctx.Target(double.NaN)` throws `ArgumentException`
  - **Impl**
    - `src/Conjecture.Core/IGeneratorContext.cs` -- add `void Target(double observation, string label = "default");`
    - `src/Conjecture.Core/ComposeStrategy.cs` (`GeneratorContext`) -- implement `Target` as `data.RecordObservation(label, observation)`
    - Update `PublicAPI.Unshipped.txt`

---

### 5.3 Hill Climber

#### Cycle 5.3.1 -- HillClimber greedy mutation
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/HillClimberTests.cs`
    - Given IR nodes `[Integer(50, 0, 100)]` and a scoring function that returns the node's value: `HillClimber.Climb` produces nodes with a higher score than the input
    - Given already-maximal nodes (value=100), `Climb` returns the same nodes
    - Given nodes with non-integer-like kinds (Boolean, Bytes), those nodes are not mutated
    - `Climb` respects `Min`/`Max` bounds — never produces out-of-range values
    - `Climb` handles multiple integer nodes — tries each independently
    - Returns `(bestNodes, bestScore)`
  - **Impl** -- `src/Conjecture.Core/Internal/HillClimber.cs`
    - `internal static class HillClimber`
    - `internal static async Task<(IReadOnlyList<IRNode> Nodes, double Score)> Climb(IReadOnlyList<IRNode> nodes, double currentScore, string label, Func<IReadOnlyList<IRNode>, Task<(Status, IReadOnlyDictionary<string, double>)>> evaluate, int budget)`
    - For each `IsIntegerLike` node: try incrementing, try decrementing, try binary-search toward Max, try binary-search toward Min. Keep the mutation that improves score most.
    - One full pass over all nodes constitutes one "round". Repeat rounds until budget exhausted or no progress.

#### Cycle 5.3.2 -- HillClimber random perturbation
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/HillClimberPerturbTests.cs`
    - HillClimber with perturbation: after greedy phase, randomly perturbs 1-3 nodes, keeps if score improves
    - A scoring function with a local maximum at value=50 but global at value=90 — perturbation helps escape local maxima within budget
    - Perturbation respects `Min`/`Max` bounds
    - Zero-budget climb does no mutations
  - **Impl** -- `src/Conjecture.Core/Internal/HillClimber.cs`
    - After greedy binary-search pass, add random perturbation sub-pass using `SplittableRandom`
    - Perturb: pick a random `IsIntegerLike` node, set to a random value in `[Min, Max]`, evaluate

---

### 5.4 Targeting Phase in TestRunner

#### Cycle 5.4.1 -- Single-label targeting
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/TestRunnerTargetingTests.cs`
    - Property that does `Target.Maximize(data.NextInteger(0, 100))` with `MaxExamples=20, Targeting=true`: after run, the max observed score is higher than the average of a pure-random run (statistical test over 5 runs with different seeds)
    - Property that never calls `Target`: targeting phase is skipped (no extra examples consumed)
    - Property with `Targeting=false`: targeting phase is skipped even if `Target.Maximize` is called
    - `TestRunResult` includes `TargetingScores` (`IReadOnlyDictionary<string, double>?`) for reporting
    - Budget split: with `MaxExamples=100` and `TargetingProportion=0.5`, generation gets ~50 examples, targeting gets ~50
    - Property that fails during generation: targeting phase is skipped, shrinking proceeds as usual
  - **Impl** -- `src/Conjecture.Core/Internal/TestRunner.cs`
    - In `RunGenerationCore`, set `Target.CurrentData.Value = data` before `test(data)`, clear in `finally`
    - Track `Dictionary<string, (IReadOnlyList<IRNode> Nodes, double Score)> bestPerLabel` during generation
    - After generation loop: if `settings.Targeting && bestPerLabel.Count > 0`, call `HillClimber.Climb` per label with remaining budget
    - If climbing finds a failing example, treat it like a generation failure (shrink and return)
    - `src/Conjecture.Core/Internal/TestRunResult.cs` -- add optional `TargetingScores` field

#### Cycle 5.4.2 -- Multi-label round-robin
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/TestRunnerMultiTargetTests.cs`
    - Property with two labels (`"size"` and `"depth"`): both are optimized
    - Budget is split evenly across labels (with N labels and B targeting budget, each label gets ~B/N rounds)
    - Property with 10 labels and `MaxExamples=20`: does not crash, each label gets at least 1 round
    - Round-robin: labels are processed in a cyclic order, not all budget to one label
  - **Impl** -- `src/Conjecture.Core/Internal/TestRunner.cs`
    - Targeting loop: `while (targetBudgetRemaining > 0) { for each label: climb 1 round, decrement budget }`
    - Each round evaluates up to `nodesCount` candidates (one per IR node mutation)

---

### 5.5 Settings

#### Cycle 5.5.1 -- Targeting settings
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/SettingsTargetingTests.cs`
    - `ConjectureSettings` default: `Targeting = true`, `TargetingProportion = 0.5`
    - `TargetingProportion = 0.0` disables targeting (all budget to generation)
    - `TargetingProportion = 1.0` is rejected (`ArgumentOutOfRangeException`) — must have at least 1 generation example
    - `TargetingProportion = -0.1` throws `ArgumentOutOfRangeException`
    - Settings flow through all four framework adapters (`PropertyAttribute` gains `Targeting` and `TargetingProportion` properties)
  - **Impl**
    - `src/Conjecture.Core/ConjectureSettings.cs` -- add `public bool Targeting { get; init; } = true;` and `public double TargetingProportion { get; init; } = 0.5;` with validation `[0.0, 1.0)`
    - `src/Conjecture.Core/ConjectureSettingsAttribute.cs` -- add nullable backing fields + `Apply()` overlay
    - `src/Conjecture.Xunit/PropertyAttribute.cs`, V3, NUnit, MSTest -- add `Targeting` and `TargetingProportion` properties
    - Update each adapter's runner to pass these through to `ConjectureSettings`
    - Update `PublicAPI.Unshipped.txt`

---

### 5.6 Failure Reporting

#### Cycle 5.6.1 -- Target scores in failure output
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Internal/TargetReportingTests.cs`
    - Failure message for a property that uses `Target.Maximize` includes `"Target scores:"` section with label and value
    - Failure message without targeting has no `"Target scores:"` section
    - Multiple labels are listed alphabetically
    - `TestRunResult.TargetingScores` is included in failure message formatting
  - **Impl**
    - `src/Conjecture.Core/Internal/CounterexampleFormatter.cs` -- when `TargetingScores` non-null and non-empty, append `"Target scores:\n  {label} = {value:F4}\n"`
    - `src/Conjecture.Core/Internal/TestCaseHelper.cs` -- pass `result.TargetingScores` to formatter

---

### 5.7 Recursive Strategy

#### Cycle 5.7.1 -- RecursiveStrategy depth-limited generation
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Strategies/RecursiveStrategyTests.cs`
    - `Generate.Recursive<int>(Generate.Just(0), self => Generate.OneOf(Generate.Just(0), self.Select(n => n + 1)), maxDepth: 3)` generates values in `[0, 3]`
    - At `maxDepth=0`, only base case values are generated
    - At `maxDepth=1`, one level of recursion is possible
    - Generated values shrink toward the base case (depth 0)
    - Works with complex types: `Expr` tree generates correctly
    - `maxDepth` negative throws `ArgumentOutOfRangeException`
    - Base case strategy that throws `UnsatisfiedAssumptionException` propagates correctly
  - **Impl** -- `src/Conjecture.Core/RecursiveStrategy.cs`
    - `internal sealed class RecursiveStrategy<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth) : Strategy<T>`
    - `Generate` method: draw an integer `[0, maxDepth]` for target depth; then recursively build using a `DepthLimitedStrategy<T>` wrapper that counts down
    - `DepthLimitedStrategy<T>` wraps the user's recursive strategy; when depth reaches 0, substitutes `baseCase`
    - Depth tracking via a depth counter integer drawn from the IR stream (so it's shrinkable)

#### Cycle 5.7.2 -- Generate.Recursive public API
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Strategies/GenRecursiveTests.cs`
    - `Generate.Recursive<T>(baseCase, recursive, maxDepth)` returns a `Strategy<T>`
    - `Generate.Recursive<T>(baseCase, recursive)` defaults to `maxDepth=5`
    - Null `baseCase` throws `ArgumentNullException`
    - Null `recursive` throws `ArgumentNullException`
    - Composes with `Select`, `Where`, `SelectMany`
    - `Generate.Compose` can use `ctx.Generate(recursiveStrategy)` correctly
  - **Impl** -- `src/Conjecture.Core/Gen.cs`
    - `public static Strategy<T> Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)`
    - Delegates to `new RecursiveStrategy<T>(baseCase, recursive, maxDepth)`
    - Update `PublicAPI.Unshipped.txt`

---

### 5.8 End-to-End Targeting Tests

#### Cycle 5.8.1 -- Targeting effectiveness
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Targeting/EndToEnd/TargetingEffectivenessTests.cs`
    - Property: `Target.Maximize(xs.Count)` where `xs : List<int>` — after targeting, max `xs.Count` observed is higher than pure-random baseline (measured over 10 seeds)
    - Property: `Target.Minimize(xs.Count)` — after targeting, min `xs.Count` is 0 (or close)
    - Property with a bug only triggered when `xs.Count > 50` — targeting finds the bug faster than random (fewer total examples on average)
    - Failure during targeting phase — produces correct shrunk counterexample with seed reproduction

#### Cycle 5.8.2 -- Targeting with database integration
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Targeting/EndToEnd/TargetingDatabaseTests.cs`
    - Failing property found via targeting: counterexample is stored in database
    - Second run replays from database successfully
    - Seed reproduction: `[Property(Seed = 0x...)]` replays the same targeting path

---

### 5.9 End-to-End Recursive Strategy Tests

#### Cycle 5.9.1 -- Expression tree generation
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Strategies/Recursive/ExprTreeTests.cs`
    - `Expr` ADT (Literal, Add, Mul) generated with `Generate.Recursive`; all generated trees have depth <= maxDepth
    - Property: `eval(expr) >= 0` for expressions over non-negative literals — planted bug (Mul of two negatives) shrinks to minimal tree
    - Shrunk tree is a base case or single-level tree (minimal depth)
    - Large maxDepth (20): does not stack overflow, generates within time bounds

#### Cycle 5.9.2 -- JSON-like structure generation
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/Strategies/Recursive/JsonValueTests.cs`
    - `JsonValue` ADT (JNull, JBool, JNumber, JString, JArray, JObject) generated recursively
    - All generated values serialize to valid JSON strings
    - Property that rejects deeply nested structures (depth > 10) is satisfied (generation respects maxDepth)
    - Shrinking reduces complex JSON to minimal failing case

---

### 5.10 Self-Tests

#### Cycle 5.10.1 -- Targeting self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/TargetingSelfTests.cs`
    - Self-test property: hill climbing on a monotonically scored property always improves or maintains score (never regresses below initial)
    - Self-test property: targeting phase never produces more examples than the budget allows
    - Self-test property: observations recorded during targeting are finite (no NaN/Infinity leaked)

#### Cycle 5.10.2 -- Recursive strategy self-tests
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/RecursiveStrategySelfTests.cs`
    - Self-test property: for any `RecursiveStrategy<int>`, generated values are reproducible from their IR nodes (replay produces same value)
    - Self-test property: shrinking a recursive value always produces a value with depth <= original depth

---

### 5.11 API Surface Tracking

#### Cycle 5.11.1 -- PublicAPI.Unshipped.txt final update
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` with zero `RS0016`/`RS0017` warnings
  - **Impl** -- `src/Conjecture.Core/PublicAPI.Unshipped.txt`
    - Add all new public API: `Target`, `Target.Maximize`, `Target.Minimize`, `IGeneratorContext.Target`, `ConjectureSettings.Targeting`, `ConjectureSettings.TargetingProportion`, `ConjectureSettingsAttribute.Targeting`, `ConjectureSettingsAttribute.TargetingProportion`, `Generate.Recursive<T>`, adapter `PropertyAttribute` properties

---

### 5.12 Performance Baselines

#### Cycle 5.12.1 -- Targeting and recursive strategy benchmarks
- [ ] `/benchmark` -- `src/Conjecture.Benchmarks/TargetingBenchmarks.cs`
  - `TargetedGeneration_SingleLabel`: throughput of 100-example run with one `Target.Maximize` call per example
  - `TargetedGeneration_MultiLabel`: throughput with 3 labels
  - `HillClimber_SingleNode`: hill climbing 100 rounds on a single integer node
  - `HillClimber_ManyNodes`: hill climbing 100 rounds on 50 integer nodes
  - `RecursiveGeneration_Depth5`: throughput of generating 1000 expression trees at maxDepth=5
  - `RecursiveGeneration_Depth10`: throughput at maxDepth=10
  - `[MemoryDiagnoser]`, `[SimpleJob]`, fixed seeds for determinism

---

### 5.13 Documentation

#### Cycle 5.13.1 -- DocFX targeted testing guide
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces no documentation warnings
  - **Impl** -- `docs/site/articles/guides/targeted-testing.md`
    - Introduction: when to use targeted testing vs pure random
    - `Target.Maximize`/`Minimize` usage examples
    - `IGeneratorContext.Target` usage inside `Generate.Compose`
    - Settings: `Targeting`, `TargetingProportion`
    - How targeting works (generation phase → hill climbing → shrinking)
    - Best practices: choosing good target metrics
    - Update `docs/site/toc.yml`

#### Cycle 5.13.2 -- DocFX recursive strategies guide
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces no documentation warnings
  - **Impl** -- `docs/site/articles/guides/recursive-strategies.md`
    - Introduction: when to use recursive strategies
    - `Generate.Recursive` API with expression tree example
    - Depth control: `maxDepth` parameter
    - Shrinking behavior: recursive values shrink toward base cases
    - JSON-like value generation example
    - Update `docs/site/toc.yml`

#### Cycle 5.13.3 -- XML doc audit on all new public API
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces zero CS1591 warnings (already gated by `TreatWarningsAsErrors`)
  - **Impl** -- Review XML doc on `Target` (class + both methods), `IGeneratorContext.Target`, `ConjectureSettings.Targeting`, `ConjectureSettings.TargetingProportion`, `Generate.Recursive<T>` (params, returns, constraints, example block)

---

## Key Constraints

- **`AsyncLocal<ConjectureData>` not `[ThreadStatic]`** — async tests require execution-context flow, not thread-local storage; `AsyncLocal` flows across `await`
- **Observations never affect shrinking** — `RecordObservation` records a score; the shrinker still operates purely on interestingness (exceptions). Targeting only influences the generation/mutation phase.
- **Hill climber only mutates `IsIntegerLike` nodes** — Bytes and Booleans are not hill-climbable. This is consistent with Python Hypothesis's approach.
- **`TargetingProportion` must be in `[0.0, 1.0)`** — at least 1 example must be generated randomly before targeting can begin. A proportion of 0.0 effectively disables targeting.
- **No new shrink pass for targeting** — targeting is generation-phase only; recursive depth shrinks via existing `IntegerReductionPass`. No new `IShrinkPass` implementation needed.
- **`Generate.Recursive` depth tracking via IR draws** — each recursion level draws an integer from the IR stream, making depth shrinkable by existing integer reduction passes.
- **No reflection in `RecursiveStrategy` or `Target`** — NativeAOT-safe (ADR-0014).
- **`Target` static class parallels `Assume` static class** — both are thin wrappers over engine state, callable from test bodies.
- **`PublicAPI.Unshipped.txt`** updated in every cycle that adds public API (ADR-0002, ADR-0003).
- **File-scoped namespaces, `sealed` on non-inheritance classes, nullable enabled, camelCase private fields** (no underscore prefix on new code).
- **Framework adapter property attributes** gain `Targeting` and `TargetingProportion` — these flow through to `ConjectureSettings` without changing the adapter's test execution model.
- Use `/decision` if design questions arise during implementation.

## New ADRs Needed

- **ADR-0035: Targeted Testing API** — `Target.Maximize`/`Minimize` static methods, `IGeneratorContext.Target`, `ConjectureData.Observations`, `AsyncLocal` ambient context, hill climber algorithm, targeting phase budget model, multiple label round-robin, database storage compatibility
- **ADR-0036: Recursive Strategy Design** — `Generate.Recursive<T>` API, `RecursiveStrategy<T>` depth-limited generation via IR integer draws, base-case substitution at depth limit, composability with existing combinators

## New Project Structure

No new projects. All implementation in existing projects:

```
src/
  Conjecture.Core/                    # Existing — add Target, RecursiveStrategy
  │                                   # Modify: Gen.cs, IGeneratorContext.cs,
  │                                   #   ComposeStrategy.cs, ConjectureSettings.cs,
  │                                   #   ConjectureSettingsAttribute.cs,
  │                                   #   PublicAPI.Unshipped.txt
  │   Internal/
  │     HillClimber.cs               # NEW
  │     ConjectureData.cs            # MODIFY (add observations)
  │     TestRunner.cs                # MODIFY (add targeting phase)
  │     TestRunResult.cs             # MODIFY (add TargetingScores)
  │     CounterexampleFormatter.cs   # MODIFY (show target scores)
  │     TestCaseHelper.cs            # MODIFY (pass target scores to formatter)
  Conjecture.Tests/
  │   Internal/
  │     ConjectureDataTargetTests.cs # NEW
  │     HillClimberTests.cs          # NEW
  │     HillClimberPerturbTests.cs   # NEW
  │     TestRunnerTargetingTests.cs  # NEW
  │     TestRunnerMultiTargetTests.cs# NEW
  │     SettingsTargetingTests.cs    # NEW
  │     TargetReportingTests.cs      # NEW
  │   TargetTests.cs                 # NEW
  │   Strategies/
  │     ComposeTargetTests.cs        # NEW
  │     RecursiveStrategyTests.cs    # NEW
  │     GenRecursiveTests.cs         # NEW
  │     Recursive/
  │       ExprTreeTests.cs           # NEW
  │       JsonValueTests.cs          # NEW
  │   Targeting/
  │     EndToEnd/
  │       TargetingEffectivenessTests.cs  # NEW
  │       TargetingDatabaseTests.cs       # NEW
  Conjecture.SelfTests/              # Existing — add targeting + recursive self-tests
  Conjecture.Benchmarks/             # Existing — add TargetingBenchmarks.cs
  Conjecture.Xunit/                  # MODIFY PropertyAttribute
  Conjecture.Xunit.V3/              # MODIFY PropertyAttribute
  Conjecture.NUnit/                  # MODIFY PropertyAttribute
  Conjecture.MSTest/                 # MODIFY PropertyAttribute
docs/
  decisions/
    0035-targeted-testing-api.md     # NEW
    0036-recursive-strategy-design.md# NEW
  site/articles/guides/
    targeted-testing.md              # NEW
    recursive-strategies.md          # NEW
```

## Critical Files

### Modified
- `src/Conjecture.Core/Internal/ConjectureData.cs` — add observations dictionary and `RecordObservation`
- `src/Conjecture.Core/Internal/TestRunner.cs` — add targeting phase, `AsyncLocal` context management
- `src/Conjecture.Core/Internal/TestRunResult.cs` — add `TargetingScores`
- `src/Conjecture.Core/Internal/CounterexampleFormatter.cs` — show target scores in failure output
- `src/Conjecture.Core/Internal/TestCaseHelper.cs` — pass target scores to formatter
- `src/Conjecture.Core/IGeneratorContext.cs` — add `Target` method
- `src/Conjecture.Core/ComposeStrategy.cs` (`GeneratorContext`) — implement `Target`
- `src/Conjecture.Core/Gen.cs` — add `Recursive<T>` factory
- `src/Conjecture.Core/ConjectureSettings.cs` — add `Targeting`, `TargetingProportion`
- `src/Conjecture.Core/ConjectureSettingsAttribute.cs` — add matching properties
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — all new API
- `src/Conjecture.Xunit/PropertyAttribute.cs` — add `Targeting`, `TargetingProportion`
- `src/Conjecture.Xunit.V3/PropertyAttribute.cs` — add `Targeting`, `TargetingProportion`
- `src/Conjecture.NUnit/PropertyAttribute.cs` — add `Targeting`, `TargetingProportion`
- `src/Conjecture.MSTest/PropertyAttribute.cs` — add `Targeting`, `TargetingProportion`

### New
- `src/Conjecture.Core/Target.cs`
- `src/Conjecture.Core/RecursiveStrategy.cs`
- `src/Conjecture.Core/Internal/HillClimber.cs`
- All test files listed per cycle above
- `docs/decisions/0035-targeted-testing-api.md`
- `docs/decisions/0036-recursive-strategy-design.md`
- `docs/site/articles/guides/targeted-testing.md`
- `docs/site/articles/guides/recursive-strategies.md`
- `src/Conjecture.Benchmarks/TargetingBenchmarks.cs`

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 5.2 (Target API):
```bash
dotnet test src/ --filter "FullyQualifiedName~Target"
```

After 5.3 (HillClimber):
```bash
dotnet test src/ --filter "FullyQualifiedName~HillClimber"
```

After 5.4 (TestRunner targeting):
```bash
dotnet test src/ --filter "FullyQualifiedName~TestRunnerTargeting"
dotnet test src/ --filter "FullyQualifiedName~TestRunnerMultiTarget"
```

After 5.7 (Recursive):
```bash
dotnet test src/ --filter "FullyQualifiedName~Recursive"
```

After 5.8 (E2E targeting):
```bash
dotnet test src/ --filter "FullyQualifiedName~EndToEnd"
```

After 5.10 (self-tests):
```bash
dotnet test src/Conjecture.SelfTests/
```

After 5.13 (docs):
```bash
dotnet build src/ -c Release
```

Final:
```bash
dotnet test src/
dotnet test src/Conjecture.SelfTests/
dotnet test src/Conjecture.Xunit.V3.Tests/
dotnet test src/Conjecture.NUnit.Tests/
dotnet test src/Conjecture.MSTest.Tests/
dotnet build src/ -c Release
```
