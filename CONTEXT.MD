# Conjecture — Domain Glossary

Claude-only artifact. Strict, opinionated dictionary of Conjecture's domain terms.
Use these terms verbatim in issues, ADRs, PR text, code, comments, and conversation.
This file overrides Hypothesis-Python vocabulary, and any informal synonyms used elsewhere.

## Rules

- **Be opinionated.** When multiple words exist for the same concept, pick the canonical one and list the rest under `_Avoid:_`.
- **Flag conflicts explicitly.** When a term is genuinely ambiguous, record it under "Flagged ambiguities" with a resolution.
- **Keep definitions tight.** One sentence. Define what it IS, not what it does.
- **Show relationships and cardinality** where it isn't obvious from the definition.
- **Only project-specific terms.** General programming concepts (timeouts, retries, error types) don't belong even if used heavily. Test: is this a concept I'd have to learn to read this codebase?
- **Group by conceptual area** (Generation, Execution, Search, IR & Shrinking, Persistence, Adapters, Internals).

## Glossary

### Generation

- **Strategy** — value-producing abstraction. `Strategy<T>` is the generic type; the non-generic `Strategy` static class exposes built-in factories (`Strategy.Integers`, `Strategy.Strings`, …). _Avoid:_ generator, generate, arbitrary, producer, gen, arb, factory.
- **IGenerationContext** — imperative draw API used inside `Compose` (`Generate<T>()`, `Assume()`, `Target()`). _Avoid:_ IGeneratorContext, context, ctx, source, sink.
- **Compose** — strategy built by drawing from other strategies imperatively via `IGenerationContext`. _Avoid:_ build, combine, monad, bind.
- **IStrategyProvider\<T\>** — source-generated strategy adapter emitted from `[Arbitrary]`. _Avoid:_ factory, registration, adapter.
- **`[Arbitrary]`** — attribute marking a type for `IStrategyProvider` generation. _Avoid:_ `[Generate]`, `[Strategy]`, `[Gen]`.

### Execution

- **Property** — a test method decorated with the framework-specific `[Property]` attribute. _Avoid:_ invariant, predicate, spec, theorem, law.
- **Example** — one complete run of a property with one drawn input and a resulting status. _Avoid:_ test case, trial, sample, iteration, attempt, run.
  _Note:_ "example" means a *completed* run. The unrun input is "the drawn value" or "the input", never an example.
- **Status** — outcome of a single example: `Valid` | `Invalid` | `Interesting` | `Overrun`. _Avoid:_ result, outcome, verdict.
- **Interesting** — Status meaning the property failed (the example is a counterexample candidate). _Avoid:_ failed, failing, bad, broken.
- **Invalid** — Status meaning an `Assume` rejected the example. _Avoid:_ rejected, filtered, skipped, discarded.
- **Overrun** — Status meaning the IR ran out during replay (mismatch between recorded and replayed draws). _Avoid:_ underflow, exhausted, truncated.
- **Assume** — assumption filter inside a property; a false assumption marks the example `Invalid`. _Avoid:_ precondition, guard, require, where.
- **Phase** — pipeline stage: `Generation` | `Targeting` | `Shrinking` | `Replay`. _Avoid:_ stage, step, mode.

### Search & Targeting

- **Target** — directs search using a numeric **observation** (`Target.Maximize` / `Target.Minimize`). _Avoid:_ optimize, score, fitness.
- **Observation** — labelled `double` recorded by a `Target` call during an example. _Avoid:_ metric, signal, sample, measurement.
- **Targeting** — phase that hill-climbs observations across labels round-robin. _Avoid:_ optimization, search, tuning.

### IR & Shrinking

- **IR** — intermediate representation; the recorded sequence of choices made during one example. _Avoid:_ buffer, choice sequence, draw log, trace, history.
- **IRNode** — atomic IR entry (`Integer`, `Boolean`, `Bytes`, `Float32`, `Float64`, `StringLength`, `StringChar`, `CommandStart`). _Avoid:_ choice, draw, decision, op, instruction.
- **Shrink** — reduce a failing IR to a simpler counterexample that still fails. _Avoid:_ minimize, reduce, simplify, narrow, contract.
- **Shrink pass** — one reduction strategy applied during shrinking (e.g. `ZeroBlocksPass`, `DeleteBlocksPass`, `LexMinimizePass`). _Avoid:_ rule, transform, step, reducer.
  _Note:_ pass class names use `Minimize`/`Reduce` for historical reasons; conversation and public terminology stays **shrink**.

### Persistence

- **Example Database** — SQLite store of failing IRs at `.conjecture/examples/`, indexed by test-id hash; replayed on next run. _Avoid:_ cache, store, repository, db, recorder.
- **Reproduction** — exported standalone failing example (`.conjecture/repros/`). _Avoid:_ repro, reproducer, replay file, regression case, snapshot.

### Adapters

- **`[Property]`** — per-framework test attribute (Xunit, Xunit.V3, NUnit, MSTest, TestingPlatform). All implement `IPropertyTest`. _Avoid:_ `[Conjecture]`, `[Test]`, `[Theory]`, `[Fact]`.
- **`[Sample]`** — attribute supplying pre-canned inputs that become Examples when run (e.g. `[Sample(1, 2, 3)]`). _Avoid:_ `[Example]`, `[InlineData]`, `[TestCase]`.
- **IPropertyTest** — common configuration interface across adapters (`MaxExamples`, `Seed`, `Database`, `MaxStrategyRejections`, `DeadlineMs`, `Targeting`, `TargetingProportion`). _Avoid:_ ITest, IConfig, IRunner.

### Internals

Names that are `internal` to `Conjecture.Core`. Use only when discussing implementation; do not surface in public docs.

- **ConjectureData** — per-example runtime state: PRNG, recorded IR, observations, status. _Avoid:_ context, state, run, recorder.
- **ShrinkState** — shrinker's per-attempt working state (current IR + shrink count). _Avoid:_ context, state.
- **TestRunner** — orchestrator that drives Phases over a Property. _Avoid:_ engine, executor, driver.

## Relationships

- A **Property** runs many **Examples**.
- An **Example** has exactly one **Status** and exactly one recorded **IR**.
- An **IR** is an ordered list of **IRNodes**.
- An `Interesting` **Example** triggers **Shrinking**, which applies many **Shrink passes** to the **IR**.
- A successfully shrunk `Interesting` **Example** is persisted to the **Example Database** and may be exported as a **Reproduction**.
- **Targeting** consumes **Observations** recorded by `Target` calls during the **Generation** phase to bias subsequent draws.
- An `[Arbitrary]`-annotated type produces an **IStrategyProvider\<T\>** at compile time, which yields a **Strategy\<T\>** at runtime.

## Flagged ambiguities

- **"Conjecture"** — both the project name and the engine name. Disambiguate as _the project_, _the engine_, or _the IR_. Never use bare "Conjecture" to mean the algorithm.
- **"reduce" vs "shrink"** — shrink-pass class names use `Minimize`/`Reduce`; the verb in conversation, comments, public API, issues, and PRs is **shrink**.
- **"example"** — refers to a *complete* run with a Status, not the raw drawn value. Use "drawn value" or "input" for the pre-execution value. _Carve-out:_ the `[Sample(args)]` attribute supplies pre-execution inputs that become Examples when run; `[Sample]` is the only place the avoid-listed word "sample" is allowed.
- **"provider"** — means `IStrategyProvider<T>` (source-gen artifact). It is *not* the Hypothesis-Python "provider" concept (alternative backend for the IR engine). If the latter ever lands, rename one of them first.
- **"generator"** — banned in prose, code, and public API. The C# port standardised on **Strategy** for both the generic type (`Strategy<T>`) and the factory (`Strategy.Integers()` etc.). The only remaining use of "generator" in the repo is the `Conjecture.Generators` source-generator project — that is now unambiguous since the `Generate` static class no longer exists.

## Example dialogue

> **Maintainer:** "The fuzzer found an `Interesting` Example on seed 0x4A2 — Phases ran Generation then Shrinking, but Shrinking stalled after three passes."
> **Claude:** "I see — the IR has a `CommandStart` IRNode the lex-minimize pass can't reorder past. Want me to add a Shrink pass that handles command boundaries, or persist the unshrunk Example to the Example Database and export a Reproduction so we can iterate offline?"
> **Maintainer:** "Persist and export. And remind me — is the failing input itself an Example?"
> **Claude:** "No. The Example is the run; the failing input is the drawn value extracted from the Example's IR. The Reproduction file embeds the IR, not the value."
