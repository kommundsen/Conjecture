# Changelog

All notable changes are documented here. See also [`CHANGELOG.md`](https://github.com/kommundsen/Conjecture/blob/main/CHANGELOG.md) in the repository root.

## [0.6.0-alpha.1] — 2026-04-05

First public alpha. All seven implementation phases complete.

### Added

- **Core engine** — byte-stream-backed generation, `SplittableRandom` PRNG, `[Property]` attribute
- **Strategy library** — integers, floats, strings, booleans, bytes, enums, collections, tuples, nullable, `SampledFrom`, `Just`, `OneOf`, `Recursive`, `StateMachine`
- **LINQ combinators** — `Select`, `Where`, `SelectMany`, `Zip`, `OrNull`, `WithLabel`, `Generate.Compose`
- **10-pass shrinking** — universal byte-stream minimization; no custom shrinkers required
- **Targeted testing** — `Target.Maximize` / `Target.Minimize` with hill-climbing phase
- **Stateful testing** — `IStateMachine<TState, TCommand>`, command sequence shrinking
- **Framework adapters** — xUnit v2, xUnit v3, NUnit 4, MSTest
- **Parameter resolution** — `[From<T>]`, `[FromFactory]`, `[Example]`, `[Arbitrary]`
- **Roslyn tooling** — source generator + 6 analyzers bundled in `Conjecture.Core`
- **Example database** — SQLite persistence of failing inputs for regression prevention
- **Structured logging** — `ILogger` integration, auto-wired in all adapters
- **Release infrastructure** — MinVer, SourceLink, GitHub Actions publish workflow
