# Phase 4 Implementation Plan: Stateful Testing & Documentation

## Context

Phase 0 delivered the core Conjecture engine (random generation, basic strategies, LINQ combinators, `[Property]` attribute, basic shrinking). Phase 1 extended with rich strategies (floats, strings, collections, choice), formatter pipeline, settings system, and SQLite example database. Phase 2 made it production-quality: 10-pass shrinker (3 tiers), `[Example]`/`[From<T>]`/`[FromFactory]` attributes, async support, enhanced failure reporting, and trim/NativeAOT validation. Phase 3 broadened developer tooling: Roslyn source generator for automatic `Arbitrary<T>` derivation, 6 Roslyn analyzers, and xUnit v3/NUnit/MSTest framework adapters. Phase 4 delivers stateful testing (ADR-0015) — the final major capability needed to match Python Hypothesis's core feature set — followed by a documentation update cycle.

The interface-based design (ADR-0015) was chosen over attribute-decorated rules (Python-style) because it is NativeAOT-safe and source-generator-friendly. Users implement `IStateMachine<TState, TCommand>` and the engine generates, executes, and shrinks command sequences automatically. No new test framework projects are needed; the feature lives entirely in `Conjecture.Core`, consumed via the existing `[Property]` attribute and `[From<T>]` parameter resolution.

**Deferred to Phase 5+:** F# API (ADR-0013), targeted testing / `Hypothesis.Target()`, recursive/tree-shaped strategies.

**End-state goal:** A user can write:

```csharp
public sealed class StackMachine : IStateMachine<Stack<int>, StackCommand>
{
    public Stack<int> InitialState() => new();

    public IEnumerable<Strategy<StackCommand>> Commands(Stack<int> state)
    {
        yield return Gen.Integers().Select(n => (StackCommand)new PushCommand(n));
        if (state.Count > 0) yield return Gen.Just((StackCommand)new PopCommand());
    }

    public Stack<int> RunCommand(Stack<int> state, StackCommand cmd) => cmd.Execute(state);

    public void Invariant(Stack<int> state) => Assert.True(state.Count >= 0);
}

[Property]
public void Stack_invariants_hold(
    [From<StateMachineStrategy<StackMachine, Stack<int>, StackCommand>>] StateMachineRun<Stack<int>> run)
    => _ = run; // failure reporting is automatic
```

And on failure, the counterexample output matches the step-sequence format from INITIAL-PLAN.MD:

```
Conjecture found a failing sequence after 38 examples (shrunk to 2 steps):

  state = InitialState();
  RunCommand(state, Push(0));
  RunCommand(state, Pop());
  Invariant(state); // ← fails here

  System.Xunit.Sdk.EqualException: ...

Reproduce with: [Property(Seed = 0x3F2A...)]
```

## Dependency Graph

```
ADR-0033 (entry point API) ──────────────────────────────────────────┐
ADR-0034 (sequence shrinking) ───────────────────────────────────┐   │
                                                                 │   │
4.1 IStateMachine + StateMachineRun + ExecutedStep ──────────────┼───┘
           │                                                     │
           v                                                     │
4.2 StateMachineStrategy (Strategy<StateMachineRun<TState>>) ────┘
    Gen.StateMachine<TMachine,TState,TCommand>()
           │
           v
4.3 StateMachineRunner (internal — executes sequence, checks Invariant)
           │
           v
4.4 CommandSequenceShrinkPass (IShrinkPass) → registered in Shrinker tier 0
           │
           v
4.5 StateMachineFormatter (IStrategyFormatter<StateMachineRun<TState>>)
    → CounterexampleFormatter picks it up automatically
           │
           v
4.6 End-to-end tests (StackMachine, QueueMachine)
           │
           v
4.7 SelfTests dogfooding (shrinker invariants over state machines)
           │
           v
4.8 API Surface Tracking (PublicAPI.Unshipped.txt)
           │
           v
4.9 Performance Baselines (BenchmarkDotNet)
           │
           v
4.10 Documentation (DocFX guide + XML doc audit)
```

## TDD Execution Plan

Each cycle: `/implement-cycle` (Red → Green → Refactor → Verify → Mark done). 11 sub-phases.

---

### 4.0 Pre-requisites

#### Cycle 4.0.1 -- ADRs
- [x] `/decision` -- ADR-0033: Stateful Test Entry Point API
  - Entry point: existing `[Property]` + `[From<StateMachineStrategy<TMachine,TState,TCommand>>]` — zero new attribute types, all existing infrastructure (seed reproduction, `[Example]`, database) works unchanged
  - `StateMachineStrategy<TMachine,TState,TCommand>` is `internal` and also implements `IStrategyProvider<StateMachineRun<TState>>` for use with `[From<T>]`
  - `Gen.StateMachine<TMachine,TState,TCommand>(int maxSteps = 50)` is the public convenience factory on `Gen.cs`
  - `StateMachineRun<TState>` (public) carries steps, failure step index, final state
  - `TMachine` must have a parameterless `new()` constraint so the engine can instantiate it
- [x] `/decision` -- ADR-0034: Command Sequence Shrinking
  - `StateMachineStrategy` marks command-start positions in the IR stream by drawing a zero-value sentinel integer with kind `IRNodeKind.CommandStart` (new enum value = 7) before each command draw; the sentinel's value is always 0 and is never used
  - `CommandSequenceShrinkPass` scans `ShrinkState.Nodes` for `CommandStart` sentinels to identify command spans
  - Three sub-passes in order: (1) truncate from end — drop last command's node span; (2) binary-halve — drop the second half of commands; (3) delete-one-at-a-time — like `DeleteBlocksPass` but at command granularity
  - After constructing a candidate node list, interestingness is delegated to `state.TryUpdate` (existing machinery); no separate simulation needed — if remaining commands produce a non-interesting run, `TryUpdate` returns false
  - Tier placement: tier 0 alongside `ZeroBlocksPass`, `DeleteBlocksPass`, `IntervalDeletionPass`
  - Update `IRNodeKind` with `CommandStart = 7`

---

### 4.1 Core Interfaces

#### Cycle 4.1.1 -- IStateMachine interface
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/IStateMachineTests.cs`
    - A concrete class implementing `IStateMachine<int, string>` compiles and satisfies the interface
    - `InitialState()` returns `TState`
    - `Commands(TState)` returns `IEnumerable<Strategy<TCommand>>`; empty enumerable when no commands applicable
    - `RunCommand(TState, TCommand)` returns a new `TState`
    - `Invariant(TState)` does not throw for valid states; throws for invalid states (any exception type accepted)
    - Interface works with both reference-type and value-type `TState` / `TCommand`
  - **Impl** -- `src/Conjecture.Core/IStateMachine.cs`
    - `public interface IStateMachine<TState, TCommand>` with four methods per ADR-0015
    - Full XML doc on each member
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 4.1.2 -- StateMachineRun and ExecutedStep
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/StateMachineRunTests.cs`
    - `StateMachineRun<TState>` constructable with list of executed steps and nullable failure step index
    - `Steps` is `IReadOnlyList<ExecutedStep<TState>>` in execution order
    - `FailureStepIndex` is null when no invariant violation occurred, or the 0-based step index on failure
    - `FinalState` is the state at the last executed step (or `InitialState()` if zero steps)
    - `Passed` is `true` iff `FailureStepIndex` is null
    - `ExecutedStep<TState>` has `State` (post-command state) and `CommandLabel` (string, for formatting)
  - **Impl** -- `src/Conjecture.Core/StateMachineRun.cs` + `src/Conjecture.Core/ExecutedStep.cs`
    - `public sealed class StateMachineRun<TState>` with all properties above
    - `public readonly record struct ExecutedStep<TState>(TState State, string CommandLabel)`
    - Update `PublicAPI.Unshipped.txt`

---

### 4.2 Command Sequence Strategy

#### Cycle 4.2.1 -- StateMachineStrategy generates state-dependent sequences
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/StateMachineStrategyTests.cs`
    - `StateMachineStrategy<TMachine,TState,TCommand>` extends `Strategy<StateMachineRun<TState>>`
    - `Generate(data)` calls `machine.InitialState()`, draws a length integer `[0, maxSteps]`, then for each step: inserts `CommandStart` sentinel, draws from `Gen.OneOf(machine.Commands(currentState))`, calls `machine.RunCommand`, calls `machine.Invariant` — records failure step and stops if invariant throws
    - With a machine whose `Commands()` always returns empty: `Steps.Count = 0`, `Passed = true`
    - With a machine whose `Invariant()` always throws: `Passed = false`, `FailureStepIndex = 0`
    - `MaxSteps = 0` always generates zero-length sequences
    - Sequence length integer is drawn first so the existing `IntegerReductionPass` naturally shrinks step count
    - `CommandLabel` for each step is `command.ToString()` (fallback) or from `FormatterRegistry`
  - **Impl** -- `src/Conjecture.Core/StateMachineStrategy.cs`
    - `internal sealed class StateMachineStrategy<TMachine, TState, TCommand> : Strategy<StateMachineRun<TState>>, IStrategyProvider<StateMachineRun<TState>> where TMachine : IStateMachine<TState, TCommand>, new()`
    - `Generate(ConjectureData data)`: draw length, loop with sentinel insertion, catch invariant exceptions
    - Also add `IRNodeKind.CommandStart = 7` to `src/Conjecture.Core/Internal/IRNodeKind.cs`

#### Cycle 4.2.2 -- Gen.StateMachine entry point
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/GenStateMachineTests.cs`
    - `Gen.StateMachine<TMachine, TState, TCommand>()` returns `Strategy<StateMachineRun<TState>>`
    - `Gen.StateMachine<TMachine, TState, TCommand>(maxSteps: 10)` respects the bound
    - Returned strategy composes with `Gen.Compose` (passes through `IGeneratorContext.Generate<T>()`)
    - Missing `new()` constraint on `TMachine` produces a compile-time error
  - **Impl** -- `src/Conjecture.Core/Gen.cs`
    - `public static Strategy<StateMachineRun<TState>> StateMachine<TMachine, TState, TCommand>(int maxSteps = 50) where TMachine : IStateMachine<TState, TCommand>, new()`
    - Delegates to `new StateMachineStrategy<TMachine, TState, TCommand>(maxSteps)`
    - Update `PublicAPI.Unshipped.txt`

---

### 4.3 State Machine Runner

#### Cycle 4.3.1 -- StateMachineRunner executes and records steps
- [x] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/StateMachineRunnerTests.cs`
    - `StateMachineRunner.Execute` for an empty command list returns `Passed = true`, `Steps.Count = 0`
    - Three-command sequence with all invariants passing: `Passed = true`, `Steps.Count = 3`, each `ExecutedStep.State` is post-command state
    - Invariant failing at step 1: `Passed = false`, `FailureStepIndex = 1`, `Steps.Count = 2`
    - Any exception type from `Invariant` is treated as failure (not just assertion exceptions)
    - `Invariant` is called after every step; commands after failure are not executed
  - **Impl** -- `src/Conjecture.Core/Internal/StateMachineRunner.cs`
    - `internal static class StateMachineRunner`
    - Pure execution function decoupled from `ConjectureData` (called by `StateMachineStrategy.Generate` after drawing the command list)

#### Cycle 4.3.2 -- Invariant failure propagates to TestRunner
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/InvariantFailurePropagationTests.cs`
    - A `[Property]` with a `StateMachineStrategy` that always violates invariant is detected and reported as failing
    - A `[Property]` with a machine whose invariant never fails passes after `MaxExamples` iterations with no exception
    - A `[Property]` with a sometimes-failing machine is found within `MaxExamples` iterations
    - Failure includes step-sequence information in the exception message (not just `StateMachineRun<T>.ToString()`)
  - **Impl** -- `src/Conjecture.Core/StateMachineStrategy.cs`
    - After `StateMachineRunner.Execute`, if `!run.Passed` rethrow the wrapped invariant exception captured during execution; triggers `TestRunner`'s existing failure path with no changes to `TestRunner`

---

### 4.4 Command Sequence Shrinking

#### Cycle 4.4.1 -- CommandSequenceShrinkPass: truncate-from-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/CommandSequenceShrinkTests.cs`
    - For an N-step failing sequence, pass truncates to N-1 steps when the last command is not the sole cause of failure
    - Truncating removes the IR node span between the last two `CommandStart` sentinels and everything after
    - Single-step failing sequence cannot be truncated; pass returns false
    - After truncation, `state.TryUpdate` confirms interestingness before accepting the candidate
  - **Impl** -- `src/Conjecture.Core/Internal/CommandSequenceShrinkPass.cs`
    - `internal sealed class CommandSequenceShrinkPass : IShrinkPass`
    - `ValueTask<bool> TryReduce(ShrinkState state)`: scan nodes for `CommandStart` sentinels, build command span index, attempt truncation from end

#### Cycle 4.4.2 -- CommandSequenceShrinkPass: binary-halve and delete-one
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/CommandSequenceShrinkPass2Tests.cs`
    - Binary-halving: 8-step sequence shrinks to 4 when the first 4 steps contain the violation
    - Delete-one: each command is tried for deletion in-order; minimal single-step failing sequence is found
    - After a full shrink pass, re-running `CommandSequenceShrinkPass` makes no further progress (idempotent)
    - State-dependent commands: deleting a command that gated a later command may make the sequence non-interesting; `TryUpdate` handles this via the existing interestingness check
  - **Impl** -- `src/Conjecture.Core/Internal/CommandSequenceShrinkPass.cs`
    - Sub-pass 2: binary-halve (remove second-half command spans)
    - Sub-pass 3: delete-one-at-a-time (like `DeleteBlocksPass` but using `CommandStart` spans as block boundaries)

#### Cycle 4.4.3 -- Register CommandSequenceShrinkPass in Shrinker tier 0
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/ShrinkIntegrationTests.cs`
    - Full `TestRunner` run with a machine that violates invariant only when three pushes are stacked — shrinks to exactly 3 steps
    - `ShrinkCount` in `TestRunResult` reflects command-level shrinks
    - Existing non-stateful property tests are unaffected (pass returns false immediately when no `CommandStart` nodes are present)
  - **Impl** -- `src/Conjecture.Core/Internal/Shrinker.cs`
    - Add `new CommandSequenceShrinkPass()` to `PassTiers[0]` (tier 0, alongside `ZeroBlocksPass`, `DeleteBlocksPass`, `IntervalDeletionPass`)

---

### 4.5 Failure Reporting

#### Cycle 4.5.1 -- StateMachineFormatter for step-sequence output
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/StateMachineFormatterTests.cs`
    - `StateMachineFormatter<TState>.Format(run)` for a passing run returns a neutral placeholder
    - For a failing run, returns multi-line string: `state = InitialState();` then `RunCommand(state, <cmd>);` per step, then `Invariant(state); // ← fails here` at the failure step
    - `<cmd>` is formatted via `FormatterRegistry.GetUntyped(cmd.GetType())` with `.ToString()` fallback
    - Formatter is registered with `FormatterRegistry` so `CounterexampleFormatter.FormatValue` picks it up automatically; no changes to `CounterexampleFormatter`
  - **Impl** -- `src/Conjecture.Core/StateMachineFormatter.cs`
    - `public sealed class StateMachineFormatter<TState> : IStrategyFormatter<StateMachineRun<TState>>`
    - Registered in `StateMachineStrategy` constructor (or static initializer) via `FormatterRegistry.Register<StateMachineRun<TState>>(...)`
    - Update `PublicAPI.Unshipped.txt`

#### Cycle 4.5.2 -- Failure message integration tests across all adapters
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Xunit.Tests/StateMachine/StateMachineReportingTests.cs` (and V3, NUnit, MSTest equivalents)
    - A failing stateful `[Property]` in each of the four framework adapters produces a failure message containing `"state = InitialState();"` and `"// ← fails here"`
    - Seed-reproduction line is still present: `Reproduce with: [Property(Seed = 0x...)]`
    - Shrink count is reported in the failure preamble
  - **Impl** -- No changes to adapters; formatter is registered globally by `StateMachineStrategy`. Tests only.

---

### 4.6 End-to-End Tests

#### Cycle 4.6.1 -- Stack state machine end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/EndToEnd/StackStateMachineTests.cs`
    - `StackMachine : IStateMachine<Stack<int>, StackCommand>` with `Push`/`Pop`; `Pop` only available when `Count > 0`; invariant asserts model count matches SUT count
    - A planted bug (count tracking off-by-one on pop) is found within 100 examples
    - Shrunk counterexample is `[Push, Pop]` — the minimal sequence revealing the bug
    - `FailureStepIndex = 1` (failure at the `Pop` step)
    - Passing variant (no bug) completes all examples without failure

#### Cycle 4.6.2 -- Queue state machine end-to-end
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.Tests/StateMachine/EndToEnd/QueueStateMachineTests.cs`
    - `QueueMachine` with `Enqueue`/`Dequeue`/`Peek`; `Dequeue`/`Peek` only when non-empty; invariant asserts FIFO ordering
    - A planted bug (peek returns wrong element after two enqueues) is found and shrunk to 3 steps
    - State-dependent availability exercised: `Dequeue` never selected when queue is empty

---

### 4.7 Self-Tests

#### Cycle 4.7.1 -- Shrinker invariant self-tests for command sequences
- [ ] `/implement-cycle`
  - **Tests** -- `src/Conjecture.SelfTests/StateMachineSelfTests.cs`
    - Self-test property: for any randomly constructed failing machine run, shrinking never increases `Steps.Count` (monotone shrinking)
    - Self-test property: shrunk run is still `Passed = false` (shrinking preserves failure)
    - Self-test property: `CommandSequenceShrinkPass` is idempotent — re-running after a full shrink makes no further progress
    - These use `Gen.Compose` to build random minimal state machines with planted bugs

---

### 4.8 API Surface Tracking

#### Cycle 4.8.1 -- PublicAPI.Unshipped.txt final update
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` with zero `RS0016`/`RS0017` warnings
  - **Impl** -- `src/Conjecture.Core/PublicAPI.Unshipped.txt`
    - Add all new public API: `IStateMachine<TState,TCommand>` (4 members), `StateMachineRun<TState>` (all properties), `ExecutedStep<TState>` (struct members), `StateMachineFormatter<TState>`, `Gen.StateMachine<TMachine,TState,TCommand>(int)`

---

### 4.9 Performance Baselines

#### Cycle 4.9.1 -- Command sequence generation and shrinking benchmarks
- [ ] `/benchmark` -- `src/Conjecture.Benchmarks/StateMachineBenchmarks.cs`
  - `StateMachineGeneration_Passing`: throughput of generating 50-step sequences for a no-failure machine
  - `StateMachineGeneration_Failing`: throughput including failure-detection path
  - `StateMachineShrinking_Short`: full find+shrink for a machine failing at step 3 of 10
  - `StateMachineShrinking_Long`: full find+shrink for a machine failing at step 20 of 50
  - `[MemoryDiagnoser]`, `[SimpleJob]`, fixed seeds for determinism; inline counter-based state machines to isolate engine overhead

---

### 4.10 Documentation

#### Cycle 4.10.1 -- DocFX stateful testing guide
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces no documentation warnings; DocFX build completes without errors
  - **Impl** -- `docs/site/articles/guides/stateful-testing.md`
    - Introduction: when to use stateful testing vs pure property tests
    - Complete `StackMachine` example from scratch (interface, commands, invariant, `[Property]` declaration)
    - Annotated failure output showing step-sequence format
    - Section: state-dependent command availability via `Commands(TState)`
    - Section: shrinking — how the engine finds the minimal failing sequence
    - Section: seed reproduction for stateful failures
    - Update `docs/site/toc.yml` (or equivalent) to include the new guide

#### Cycle 4.10.2 -- XML doc audit on all new public API
- [ ] `/implement-cycle`
  - **Tests** -- `dotnet build src/ -c Release` produces zero CS1591 warnings (already gated by `TreatWarningsAsErrors`)
  - **Impl** -- Review XML doc on `IStateMachine<TState,TCommand>` (all four methods + type params), `StateMachineRun<TState>` (all properties), `ExecutedStep<TState>`, `StateMachineFormatter<TState>`, `Gen.StateMachine<>()` (params, returns, constraints, example block)

---

## Key Constraints

- **Interface shape is fixed** per ADR-0015: `IStateMachine<TState, TCommand>` with four methods; do not add overloads or optional members
- **No new framework adapter projects** — stateful testing lives entirely in `Conjecture.Core`; existing adapters need no changes beyond the formatter being globally registered
- **`IShrinkPass.TryReduce` is `ValueTask<bool>`** — `CommandSequenceShrinkPass` must be async, matching the existing pass interface
- **`IRNodeKind.CommandStart = 7`** must be added to `IRNodeKind.cs` before `StateMachineStrategy` is implemented
- **`Gen.StateMachine<>()` requires `new()` constraint** on `TMachine` — enforced at compile time; runtime instantiation uses `new TMachine()`
- **`PublicAPI.Unshipped.txt`** updated in every cycle that adds public API (ADR-0002, ADR-0003)
- **File-scoped namespaces, `sealed` on non-inheritance classes, nullable enabled, camelCase private fields** (no underscore prefix on new code)
- **No reflection in `StateMachineStrategy`** — `new TMachine()` not `Activator.CreateInstance`; NativeAOT-safe (ADR-0014)
- **`StateMachineStrategy` is `internal`** — exposed only via `Gen.StateMachine<>()` and `IStrategyProvider<StateMachineRun<TState>>` for `[From<T>]` use; not in `PublicAPI.Unshipped.txt`
- **Formatter registered globally** so `CounterexampleFormatter` picks it up with no changes to reporting infrastructure
- Use `/decision` if design questions arise during implementation

## New ADRs Needed

- **ADR-0033: Stateful Test Entry Point API** — `[Property]` + `[From<StateMachineStrategy<>>]` entry point, `Gen.StateMachine<>()` factory, `StateMachineRun<TState>` result type, `new()` constraint rationale, seed reproduction compatibility
- **ADR-0034: Command Sequence Shrinking** — `IRNodeKind.CommandStart` sentinel approach, three sub-passes (truncate-end, binary-halve, delete-one), tier 0 placement, interestingness check delegation to existing `ShrinkState.TryUpdate`

## New Project Structure

No new projects. All implementation in existing projects:

```
src/
  Conjecture.Core/                    # Existing — add IStateMachine, StateMachineRun,
  │                                   #   ExecutedStep, StateMachineStrategy, StateMachineFormatter
  │                                   # Modify: Gen.cs, IRNodeKind.cs, Shrinker.cs,
  │                                   #   PublicAPI.Unshipped.txt
  │   Internal/
  │     StateMachineRunner.cs         # NEW
  │     CommandSequenceShrinkPass.cs  # NEW
  Conjecture.Tests/
  │   StateMachine/                   # NEW: unit + integration tests
  │     EndToEnd/                     # NEW: StackMachine, QueueMachine
  Conjecture.SelfTests/               # Existing — add StateMachineSelfTests.cs
  Conjecture.Benchmarks/              # Existing — add StateMachineBenchmarks.cs
  Conjecture.Xunit.Tests/             # Existing — add StateMachine/reporting test
  Conjecture.Xunit.V3.Tests/          # Existing — add StateMachine/reporting test
  Conjecture.NUnit.Tests/             # Existing — add StateMachine/reporting test
  Conjecture.MSTest.Tests/            # Existing — add StateMachine/reporting test
docs/
  site/articles/guides/
    stateful-testing.md               # NEW
```

## Critical Files

### Modified
- `src/Conjecture.Core/Gen.cs` — add `StateMachine<TMachine,TState,TCommand>()` factory
- `src/Conjecture.Core/Internal/IRNodeKind.cs` — add `CommandStart = 7`
- `src/Conjecture.Core/Internal/Shrinker.cs` — add `CommandSequenceShrinkPass` to `PassTiers[0]`
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` — add all new public API signatures

### New
- `src/Conjecture.Core/IStateMachine.cs`
- `src/Conjecture.Core/StateMachineRun.cs`
- `src/Conjecture.Core/ExecutedStep.cs`
- `src/Conjecture.Core/StateMachineStrategy.cs`
- `src/Conjecture.Core/StateMachineFormatter.cs`
- `src/Conjecture.Core/Internal/StateMachineRunner.cs`
- `src/Conjecture.Core/Internal/CommandSequenceShrinkPass.cs`
- `src/Conjecture.Tests/StateMachine/IStateMachineTests.cs`
- `src/Conjecture.Tests/StateMachine/StateMachineRunTests.cs`
- `src/Conjecture.Tests/StateMachine/StateMachineStrategyTests.cs`
- `src/Conjecture.Tests/StateMachine/GenStateMachineTests.cs`
- `src/Conjecture.Tests/StateMachine/StateMachineRunnerTests.cs`
- `src/Conjecture.Tests/StateMachine/InvariantFailurePropagationTests.cs`
- `src/Conjecture.Tests/StateMachine/CommandSequenceShrinkTests.cs`
- `src/Conjecture.Tests/StateMachine/CommandSequenceShrinkPass2Tests.cs`
- `src/Conjecture.Tests/StateMachine/ShrinkIntegrationTests.cs`
- `src/Conjecture.Tests/StateMachine/StateMachineFormatterTests.cs`
- `src/Conjecture.Tests/StateMachine/EndToEnd/StackStateMachineTests.cs`
- `src/Conjecture.Tests/StateMachine/EndToEnd/QueueStateMachineTests.cs`
- `src/Conjecture.SelfTests/StateMachineSelfTests.cs`
- `src/Conjecture.Benchmarks/StateMachineBenchmarks.cs`
- `src/Conjecture.Xunit.Tests/StateMachine/StateMachineReportingTests.cs`
- `src/Conjecture.Xunit.V3.Tests/StateMachine/StateMachineReportingTests.cs`
- `src/Conjecture.NUnit.Tests/StateMachine/StateMachineReportingTests.cs`
- `src/Conjecture.MSTest.Tests/StateMachine/StateMachineReportingTests.cs`
- `docs/site/articles/guides/stateful-testing.md`

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 4.2 (strategy):
```bash
dotnet test src/ --filter "FullyQualifiedName~StateMachineStrategy"
```

After 4.4 (shrinking):
```bash
dotnet test src/ --filter "FullyQualifiedName~ShrinkPass"
dotnet test src/ --filter "FullyQualifiedName~ShrinkIntegration"
```

After 4.6 (E2E):
```bash
dotnet test src/ --filter "FullyQualifiedName~EndToEnd"
```

After 4.7 (self-tests):
```bash
dotnet test src/Conjecture.SelfTests/
```

After 4.10 (docs):
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
