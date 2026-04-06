# Draft: Microsoft.Testing.Platform Native Adapter

## Motivation

.NET 10 SDK natively supports Microsoft.Testing.Platform (MTP) as a first-class test runner via `global.json` configuration. MTP offers faster test execution, structured output, and a modern extensibility model. Conjecture currently ships adapters for xUnit v2, xUnit v3, NUnit, and MSTest — all running atop VSTest. A native MTP adapter would make Conjecture a first-class citizen in the modern .NET testing ecosystem.

## .NET Advantage

.NET 10 introduces Microsoft.Testing.Platform as a first-class, pluggable test execution model with structured output, custom CLI arguments, and native `dotnet test` integration. This architecture lets Conjecture participate as a test engine — controlling discovery, execution, parallelism, and result reporting — rather than only operating as a library consumed by a separate runner.

## Key Ideas

- New project: `Conjecture.TestingPlatform` targeting `net10.0`
- Implements `Microsoft.Testing.Platform` interfaces directly (no VSTest bridge)
- `[Property]` attribute discovered via MTP's test discovery protocol
- Native support for:
  - Structured test results with counterexample details
  - Parallel property execution with configurable degree
  - Progress reporting during generation/shrinking
  - Custom CLI arguments (`--conjecture-seed`, `--conjecture-max-examples`)
- `dotnet test` works out of the box with `global.json` runner config
- Could eventually replace the framework-specific adapters for users who don't need xUnit/NUnit/MSTest features

## Design Decisions to Make

1. Ship as a standalone package (`Conjecture.TestingPlatform`) or integrate into Core?
2. How to handle mixed solutions where some projects use xUnit and some use MTP directly?
3. Should MTP adapter support `[Example]` attribute replay?
4. How to wire `ILogger` to MTP's output infrastructure?
5. MTP version minimum: 1.7+ required for .NET 10 SDK native support
6. Should this adapter support `Microsoft.Testing.Extensions.VSTestBridge` for backward compat?

## Scope Estimate

Medium-Large. MTP has a well-defined extension model but requires implementing discovery, execution, and result reporting from scratch. ~3-4 cycles.

## Dependencies

- `Microsoft.Testing.Platform` NuGet package (v1.7+)
- `Microsoft.Testing.Platform.MSBuild` for `dotnet test` integration
- Existing `SharedParameterStrategyResolver` from Core
- .NET 10 SDK

## Open Questions

- What MTP extensions should we support out of the box? (e.g., `Microsoft.Testing.Extensions.Retry`, `Microsoft.Testing.Extensions.CrashDump`)
- How do IDE test explorers (VS 2026, VS Code) display property test results vs unit test results?
- Should we support MTP's `--list-tests` for property tests? (Properties are parameterized — what does "listing" mean?)
