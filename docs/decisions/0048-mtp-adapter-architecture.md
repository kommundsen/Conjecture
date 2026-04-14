# 0048. MTP Adapter Architecture

**Date:** 2026-04-14
**Status:** Accepted

## Context

Microsoft Testing Platform (MTP) is a modern, self-contained test execution platform that embeds the runner directly in the test project executable. Unlike VSTest — which launches a separate `testhost` process — an MTP test project sets `OutputType=Exe` and references the platform directly. MTP 1.x is the engine behind `dotnet test` in .NET 9+ and is the target platform for TRX reporting, crash dump collection, and IDE integrations going forward.

Conjecture already supports xUnit v2/v3, NUnit, and MSTest via dedicated adapter packages (ADR-0030). Each of those packages targets a framework-specific extensibility model and ships as a library (`OutputType=Library`). MTP's executable output requirement makes it incompatible with those packages: a library cannot set `OutputType=Exe`, and Core cannot carry this constraint without forcing it on every consumer.

MTP provides two integration paths:

1. **VSTestBridge** — a compatibility shim that translates VSTest discovery/execution calls into MTP's protocol. Low effort to adopt; carries the full weight of the VSTest adapter model with its known limitations (no fine-grained `TestNode` control, no native capability support).
2. **Native `ITestFramework`** — direct implementation of MTP's extension point. Full control over `TestNode` shape, lifecycle events, and platform capabilities. More code, but unlocks TRX reporting, crash dump integration, and precise test node composition.

The question is which path to take, how to package it, and how to wire Conjecture's existing infrastructure (logging, `[Example]` parity, seed/settings) into MTP's protocol.

## Decision

### Standalone package: `Conjecture.TestingPlatform`

Ship MTP support as a separate `Conjecture.TestingPlatform` NuGet package. Users reference it alongside their chosen Conjecture adapter (or instead of it, if they run MTP-first). This package sets no `OutputType` itself — test projects that reference it set `OutputType=Exe` in their own `.csproj`, which is standard MTP usage.

This keeps `Conjecture.Core` a plain library and preserves the opt-in model established for all other adapters.

### Native `ITestFramework` — no VSTestBridge

Implement `ITestFramework` directly. VSTestBridge is a stepping stone for frameworks that already have a VSTest adapter and want MTP reach without rewriting; Conjecture has no VSTest adapter to bridge from. A native implementation gives full control over:

- `TestNode` composition (one node per `[Property]`, child nodes per `[Example]`)
- `ITestSessionContext` and lifetime hooks
- Platform capabilities (TRX, crash dump) declared at startup

### MTP minimum version: 1.9

MTP 1.9 is the first release with stable `ITrxReportCapability` and the `KeyValuePairStringProperty` API used for structured log output. Targeting 1.9 avoids conditional compilation around capability availability.

### Logging: `IMessageBus` + `KeyValuePairStringProperty`

MTP has no direct equivalent of xUnit's `ITestOutputHelper` or NUnit's `TestContext.Out`. The idiomatic pattern is to attach structured properties to `TestNode` updates via `IMessageBus`. A `MtpLogger` wraps `IMessageBus` and posts `KeyValuePairStringProperty("output", message)` on each log line, mirroring what `TestOutputHelperLogger` does in the xUnit adapter.

### `TestNode` structure

Each `[Property]` method produces one `TestNode`. `[Example]` cases that run before random generation produce child `TestNode` entries under the property node. This structure maps naturally to IDE Test Explorer hierarchies and TRX output sections.

### `[Example]` parity via `TestCaseHelper.ValidateExampleArgs()`

`TestCaseHelper.ValidateExampleArgs()` in Core validates and unpacks `[Example]` attribute arguments into typed parameter arrays. The MTP adapter calls this method directly (via `InternalsVisibleTo`), giving identical `[Example]` semantics to every other adapter without duplicating validation logic.

### Discovery: method names only

`DiscoverTestExecutionRequest` returns one entry per `[Property]` method. Discovered tests list only the method name — not generated inputs or `[Example]` argument permutations. This matches how other adapters surface discovery and avoids flooding the test list with unbounded generated cases.

### Out-of-box extensions: TrxReport and CrashDump

`Conjecture.TestingPlatform` registers `ITrxReportCapability` and the crash dump extension at startup. Both are available without any user configuration. TRX output goes to the standard `TestResults/` directory; crash dumps are captured on unhandled exceptions during test execution.

### Mixed-solution support

A solution may contain test projects targeting different frameworks (xUnit, NUnit, MSTest) alongside MTP test projects. Each test project selects its own adapter via its project references. No global configuration is required; MTP test projects simply reference `Conjecture.TestingPlatform` while other projects reference their respective adapter packages.

## Consequences

**Easier:**
- Core remains a library with no `OutputType` constraint; all existing consumers are unaffected.
- Full `TestNode` control enables precise TRX and IDE integration without workarounds.
- Logging, `[Example]` parity, and seed/settings infrastructure reuse Core without duplication.
- Mixed adapter solutions work without any glue configuration.

**Harder:**
- `Conjecture.TestingPlatform` is a net-new project with its own `PublicAPI.Unshipped.txt`, test project, and CI steps.
- MTP's native API surface is less documented than the framework-specific models; implementation requires consulting the MTP source and samples directly.
- `InternalsVisibleTo` must be extended to include `Conjecture.TestingPlatform` for `TestCaseHelper` and `SharedParameterStrategyResolver` access.

## Alternatives Considered

**VSTestBridge** — rejected. Conjecture has no existing VSTest adapter, so VSTestBridge offers no migration advantage. It also prevents native `ITrxReportCapability` registration and limits `TestNode` shape control.

**Integrate MTP support into `Conjecture.Core`** — rejected. MTP's executable output requirement would force all Core consumers to set `OutputType=Exe`, breaking library consumers and every existing adapter.

**Target MTP 1.5 (earlier stable release)** — rejected. `KeyValuePairStringProperty` and `ITrxReportCapability` stabilised in 1.9; targeting an earlier version would require runtime capability probing or conditional compilation.

**Per-framework MTP shims (e.g., `Conjecture.Xunit.Mtp`)** — rejected. MTP is framework-agnostic at the execution layer; a single `Conjecture.TestingPlatform` package is the correct boundary. Framework-specific shims would duplicate test runner logic and fragment the package surface.
