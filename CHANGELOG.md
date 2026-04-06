# Changelog

All notable changes to Conjecture.NET are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [SemVer](https://semver.org/) — API stability guarantees begin at v1.0.0.

---

## [Unreleased]

---

## [0.6.0-alpha.1] — 2026-04-05

First public alpha release. All seven implementation phases are complete.

### Added

**Core engine**
- `ConjectureData` byte-stream-backed test case generation
- `Strategy<T>` abstract base with `Draw(ConjectureData)` semantics
- `SplittableRandom` (SplitMix64) reproducible PRNG
- `Assume.That(condition)` for filtering
- `[Property]` attribute with auto-resolved parameter strategies

**Strategy library**
- Primitives: `Generate.Booleans()`, `Generate.Bytes(size)`, `Generate.Integers<T>()`, `Generate.Doubles()`, `Generate.Floats()`
- Strings: `Generate.Strings(...)`, `Generate.Text(...)`
- Collections: `Generate.Lists<T>()`, `Generate.Sets<T>()`, `Generate.Dictionaries<K,V>()`
- Combinators: `Select`, `Where`, `SelectMany`, `Zip`, `OrNull`, `WithLabel`
- Choice: `Generate.Just()`, `Generate.OneOf()`, `Generate.SampledFrom()`, `Generate.Enums<T>()`
- Composition: `Generate.Compose(ctx => ...)` imperative builder
- Recursive: `Generate.Recursive<T>(baseCase, recursive, maxDepth)`
- Stateful: `Generate.StateMachine<TMachine, TState, TCommand>(maxSteps)`

**Shrinking**
- 10-pass byte-stream shrinking (ZeroBlocks, DeleteBlocks, LexMinimize, IntegerReduction, FloatSimplification, StringAware, BlockSwapping, CommandSequence, and more)
- No custom shrinker code required — works universally for all types

**Targeted testing**
- `Target.Maximize(score, label)` / `Target.Minimize(score, label)`
- `IGeneratorContext.Target(score, label)` inside `Generate.Compose`
- Hill-climbing phase after random generation

**Stateful testing**
- `IStateMachine<TState, TCommand>` interface
- Command sequence generation and shrinking
- `StateMachineRun<TState>` result with step-by-step failure reporting

**Test framework adapters**
- `Conjecture.Xunit` — xUnit v2
- `Conjecture.Xunit.V3` — xUnit v3
- `Conjecture.NUnit` — NUnit 4
- `Conjecture.MSTest` — MSTest

**Parameter resolution attributes**
- `[From<TProvider>]` — custom `IStrategyProvider<T>`
- `[FromFactory(methodName)]` — static factory method
- `[Example(args)]` — explicit test cases run before generated ones
- `[Arbitrary]` — source generator marker

**Roslyn tooling** (bundled in `Conjecture.Core`)
- Source generator: auto-derives `IStrategyProvider<T>` for `[Arbitrary]` types
- 6 analyzers: CON100–CON105
- Code fixes for common diagnostics

**Example database**
- SQLite-backed persistence of failing byte buffers
- Automatic replay on subsequent runs for regression prevention

**Structured logging**
- `ILogger` integration via `ConjectureSettings.Logger`
- Auto-wired to framework output in all four adapters
- 12 structured log events covering generation, shrinking, and targeting

**Release infrastructure**
- MinVer tag-based versioning
- SourceLink + deterministic builds
- GitHub Actions release workflow (`v*` tag → NuGet publish)
- Public API tracking (`PublicAPI.Shipped.txt`)

[0.6.0-alpha.1]: https://github.com/kommundsen/Conjecture/releases/tag/v0.6.0-alpha.1
