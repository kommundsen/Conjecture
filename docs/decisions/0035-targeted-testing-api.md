# 0035. Targeted Testing API

**Date:** 2026-04-03
**Status:** Accepted

## Context

Phase 5 adds targeted property testing — Python Hypothesis's `hypothesis.target()` feature. Users provide numeric observations during test execution; the engine hill-climbs on the IR buffer to maximize/minimize scores, steering generation toward interesting regions of the input space. This is the last major engine-level feature needed to match Hypothesis's core.

The existing generation loop is purely random: it draws examples from strategies and checks for failures. For properties where interesting behavior is concentrated in a small region of the input space (e.g., large collections, specific numeric ranges, deep trees), random generation may take many examples to find failures. Targeted testing addresses this by letting users provide a numeric "score" that the engine optimizes, directing generation toward high-scoring regions.

## Decision

### Public API

Two exposure paths for recording observations:

1. **`Target` static class** (parallels `Assume`): `Target.Maximize(double observation, string label = "default")` and `Target.Minimize(double observation, string label = "default")` — callable from `[Property]` test bodies.
2. **`IGeneratorContext.Target(double observation, string label = "default")`** — callable inside `Generate.Compose` blocks.

`Minimize(x, label)` is sugar for `Maximize(-x, label)`.

### Ambient Context

`Target` uses `AsyncLocal<ConjectureData?>` (not `[ThreadStatic]`) to reach the current `ConjectureData`. `AsyncLocal` is required because tests can be async — `[ThreadStatic]` does not flow across `await` continuations. `TestRunner` sets `Target.CurrentData.Value = data` before invoking the test and clears it in `finally`. Calling `Target.Maximize` outside a test context throws `InvalidOperationException`.

### Observation Storage

`ConjectureData` gains `Dictionary<string, double> observations` and `RecordObservation(string label, double value)`. Validates: not frozen, not NaN, not Infinity. Multiple calls with the same label overwrite (last value wins). Observations are readable after `Freeze()`.

### Targeting Phase

`TestRunner.RunGenerationCore` gains a targeting phase between the generation loop and the return statement:

1. During generation, track `Dictionary<string, (IReadOnlyList<IRNode> Nodes, double Score)> bestPerLabel` — updated whenever an example's observation for a label exceeds the current best.
2. After the generation loop, if `settings.Targeting && bestPerLabel.Count > 0`: enter targeting phase with remaining budget (`(int)(MaxExamples * TargetingProportion)`).
3. Targeting phase round-robins across labels: `while (budget > 0) { foreach label: hill-climb 1 round, decrement budget }`.
4. If a test failure is found during targeting, shrink and return as a failure — same path as generation failures.

### Hill Climber

`HillClimber.Climb(nodes, score, label, evaluate, budget)` — internal static class.

- **Greedy phase:** For each `IsIntegerLike` node, try increment/decrement and binary-search toward Min/Max. Keep the mutation that improves score most.
- **Perturbation phase:** Pick a random `IsIntegerLike` node, set to a random value in `[Min, Max]` using `SplittableRandom`. Accept if score improves.
- One full pass over all nodes = one "round". Repeat until budget exhausted or no progress in a full round.
- Boolean and Bytes nodes are not mutated (not meaningfully hill-climbable).

### Settings

`ConjectureSettings` gains:

- `Targeting` (`bool`, default `true`) — master switch for the targeting phase.
- `TargetingProportion` (`double`, default `0.5`, validated `[0.0, 1.0)`) — fraction of `MaxExamples` budget reserved for the targeting phase. `0.0` effectively disables targeting. Must be `< 1.0` to ensure at least 1 random generation example.

Framework adapter `PropertyAttribute` classes gain matching properties that flow through to `ConjectureSettings`.

### Shrinking Interaction

Observations are purely advisory — they never affect shrinking. Shrinking operates on interestingness (exceptions) only. If a failure is found during the targeting phase, the failing IR nodes are shrunk via the existing `Shrinker.ShrinkAsync` path with no changes to the shrinker.

### Failure Reporting

`TestRunResult` gains optional `TargetingScores` (`IReadOnlyDictionary<string, double>?`). `CounterexampleFormatter` appends a "Target scores:" section when present and non-empty, listing each label and its best score alphabetically.

## Consequences

- Enables coverage-guided generation for complex domains where random sampling is insufficient
- No new shrink pass needed — targeting is generation-phase only
- `AsyncLocal` adds negligible overhead per test invocation (~tens of nanoseconds)
- Multiple labels allow users to optimize orthogonal metrics simultaneously
- Budget split means fewer purely random examples when targeting is active — acceptable tradeoff since targeting explores more intelligently
- NativeAOT-safe: no reflection involved
- `Target` static class mirrors the `Assume` pattern — familiar to existing users
- Hill climber is simple and deterministic (given a seed) — easy to debug and reproduce

## Alternatives Considered

1. **`[ThreadStatic]` instead of `AsyncLocal`** — simpler but breaks async tests. `await` can resume on a different thread, losing the thread-static value. Rejected.
2. **Pass `ConjectureData` explicitly to user tests** — would require changing the `[Property]` method signature to accept a context parameter. Breaks the clean `[Property] void Test(int x)` pattern. Rejected.
3. **Observations affect shrinking (shrink toward high scores)** — adds complexity to the shrinker for unclear benefit. Shrinking should minimize the counterexample, not maximize a score. Rejected.
4. **Single label only** — simpler but limits expressiveness. Users often want to optimize multiple independent metrics (e.g., collection size and value diversity). Multiple labels with round-robin adds modest complexity. Rejected.
5. **Separate `TargetingExamples` count instead of proportion** — harder for users to reason about; proportion is more intuitive and scales with `MaxExamples`. Rejected.
