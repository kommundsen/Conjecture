# Changelog

All notable changes to Conjecture are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [SemVer](https://semver.org/) — API stability guarantees begin at v1.0.0.

---

## [Unreleased]

---

## [0.28.0] — 2026-05-09

### Added

**Core** (`Conjecture.Core`)
- `Strategy.Indices(int maxValue)` and `Strategy.Ranges(int maxValue)` — generate `System.Index` and `System.Range` values bounded by `maxValue`
- `Strategy.Halves()` and `Strategy.Halves(Half min, Half max)` — generate `System.Half` (16-bit IEEE 754 float)
- `Strategy.Integers(BigInteger min, BigInteger max)` — ranged generator for `System.Numerics.BigInteger`; shrinks toward whichever bound is closest to zero
- `Strategy.Runes()` and `Strategy.Runes(Rune min, Rune max)` — generate `System.Text.Rune` across the full Unicode scalar range
- `Strategy.IPAddresses(IPAddressKind)`, `Strategy.IPEndPoints()`, `Strategy.Uris()`, `Strategy.EmailAddresses()`, `Strategy.EmailAddressStrings()` — network and email address strategies
- `Strategy.Compose<T>(Func<IGenerationContext, T>)` — compose a strategy from multiple child draws; `IGenerationContext` and `PartialConstructorContext` expose `Generate<T>`, `Assume`, and `Target`
- `SeededStrategy<T>` record and `StrategySamplingExtensions` — `.Sample()`, `.Sample(count)`, `.Stream()`, `.Stream(count)`, and `.WithSeed(seed)` extension methods on any `Strategy<T>`
- `SampleAttribute`, `StrategyRangeAttribute`, `StrategyStringLengthAttribute`, `StrategyRegexAttribute`, `StrategyMaxDepthAttribute` — data-annotation attributes for property customisation
- `FromMethodAttribute` — replaces `FromFactoryAttribute` (removed) to reference a factory method by name
- `ConjectureSettings.Database`, `.ExportReproductionOnFailure`, `.ReproductionOutputPath` — opt-in database caching and failure-reproduction export settings
- `IPAddressKind` enum (`V4`, `V6`, `Both`)

**Core.Abstractions** (`Conjecture.Core.Abstractions`) — new package
- `IStrategyFormatter<T>` — moved from `Conjecture.Core`; decoupled formatter contract

**Testing.Abstractions** (`Conjecture.Testing.Abstractions`) — new package
- `IPropertyTest` — moved from `Conjecture.Core`; decoupled property-test attribute contract including new `Database` member
- `IReproductionExport` — moved from `Conjecture.Core`; contract for `ExportReproductionOnFailure` and `ReproductionOutputPath`
- `TestOutputLogger` — `ILogger` implementation wrapping any `Action<string>` write-line delegate, with `FromWriteLine` factory

**Interactions.Abstractions** (`Conjecture.Interactions.Abstractions`) — new package
- `IInteraction`, `IAddressedInteraction`, `IInteractionTarget`, `InteractionStateMachine<TState>` — decoupled interaction contracts (previously lived in `Conjecture.Interactions`)

**JsonSchema.Abstractions** (`Conjecture.JsonSchema.Abstractions`) — new package
- `JsonSchemaNode` record, `JsonSchemaType` enum, `JsonSchemaParser` — decoupled JSON Schema IR; allows downstream packages to depend on the schema model without pulling in the generator

**Aspire.Abstractions** (`Conjecture.Aspire.Abstractions`) — new package
- `IAspireAppFixture` — moved from `Conjecture.Aspire`; decoupled fixture contract with `StartAsync`, `ResetAsync`, `WaitForHealthyAsync`, `DisposeAsync`, `HealthCheckedResources`, `MaxRetryAttempts`, `RetryDelay`
- `ISnapshotLabel` — marker interface for snapshot label types

**Aspire.Http** (`Conjecture.Aspire.Http`) — new package
- `AspireHttpProperty.RunAsync` — property runner combining an `IAspireAppFixture` with an `InteractionStateMachine<TState>` over HTTP
- `DistributedApplicationHttpTarget` — `IInteractionTarget` backed by a `DistributedApplication`, resolving `HttpClient` per resource name
- `HttpInteractionTraceReporter` — records HTTP interactions and formats a trace report

**Aspire** (`Conjecture.Aspire`)
- `AspireProperty.RunAsync` — new overload accepting a generic `InteractionStateMachine<TState>` and a `targetFactory`, superseding the old `AspireStateMachine<TState>` overload
- `AppLogTailFormatter.Format(DistributedApplication?)` — static helper formatting the tail of the distributed application log for failure output

**Aspire.EFCore** (`Conjecture.Aspire.EFCore`)
- `SnapshotTraceReporter` — records per-step snapshots and formats a structured report via `RecordSnapshot` / `FormatReport`

**Mcp** (`Conjecture.Mcp`)
- `suggest-strategy` recognises `Index`, `Range`, `Half`, `Int128`, `UInt128`, `BigInteger`, and `Rune`

**Test frameworks** (Xunit, Xunit.V3, NUnit, MSTest, TestingPlatform)
- `PropertyAttribute.Database`, `.ExportReproductionOnFailure`, `.ReproductionOutputPath` — new settings properties on all test framework `PropertyAttribute` types

**Time** (`Conjecture.Time`)
- `Strategy.CulturesByCalendar<TCalendar>()` — cultures whose default calendar is exactly `TCalendar`
- `Strategy.CulturesNonGregorian()` — non-Gregorian cultures (Hijri, Hebrew, Japanese-era, Persian, Thai-Buddhist)

### Changed

**Interactions** (`Conjecture.Interactions`)
- `CompositeInteractionTarget` and `Property.ForAll` overloads now accept `Conjecture.Abstractions.Interactions` types instead of the old concrete types in `Conjecture.Interactions`

**Grpc** (`Conjecture.Grpc`), **Http** (`Conjecture.Http`), **Messaging** (`Conjecture.Messaging`), **JsonSchema** (`Conjecture.JsonSchema`), **OpenApi** (`Conjecture.OpenApi`), **Protobuf** (`Conjecture.Protobuf`), **EFCore** (`Conjecture.EFCore`), **Money** (`Conjecture.Money`), **Time** (`Conjecture.Time`), **Regex** (`Conjecture.Regex`)
- Extension entry-point classes renamed from `Generate`-prefixed to `Strategy`-suffixed convention (e.g. `HttpGenerateExtensions` → `HttpStrategyExtensions`, `GenerateGrpc` → `GrpcStrategyExtensions`). New companion builder/accessor types introduced: `MessagingStrategies`, `DbStrategies`, `DbInteractionSequenceBuilder`

**Money** (`Conjecture.Money`)
- `MoneyStrategyExtensions` (née `MoneyGenerateExtensions`) adds `CulturesWithCurrency()` and `CulturesByCurrencyCode(string)` — filter cultures by associated ISO 4217 currency

### Removed

**Core** (`Conjecture.Core`)
- `FromFactoryAttribute` — use `FromMethodAttribute` instead
- `IPropertyTest`, `IReproductionExport`, `IStrategyFormatter<T>` — moved to `Conjecture.Testing.Abstractions` / `Conjecture.Core.Abstractions`

**Interactions** (`Conjecture.Interactions`)
- `IInteraction`, `IAddressedInteraction`, `IInteractionTarget`, `InteractionStateMachine<TState>` — moved to `Conjecture.Interactions.Abstractions`

**Aspire** (`Conjecture.Aspire`)
- `IAspireAppFixture`, `AspireStateMachine<TState>`, `Interaction` — moved to `Conjecture.Aspire.Abstractions`; `AspireStateMachine` superseded by the generic `InteractionStateMachine<TState>`

---

## [0.27.1] — 2026-04-28

### Added

**EFCore** (`Conjecture.EFCore`)
- `IDbTarget.ResetAsync(resourceName, ct)` — interface contract for resetting a target's data state for the named resource

**Aspire.EFCore** (`Conjecture.Aspire.EFCore`) — new package
- `AspireDbTarget<TContext>` — `IDbTarget` resolving a `DbContext` against an Aspire-managed resource via `ConnectionStringResolver`, with `CreateAsync` overloads taking either a `DistributedApplication` or a `ConnectionStringResolver`
- `AspireDbTargetRegistry` and `AspireDbFixtureExtensions.CreateDbRegistry` — registry of `IDbTarget`s with bulk `ResetAllAsync` and `IAsyncDisposable` lifetime
- `IDbTargetWaitForExtensions.WaitForAsync` (typed and `DbContext` overloads) — polls a predicate on the resolved context until satisfied or timeout
- `AspireEFCoreInvariants` — composite asserter over an `IInteractionTarget` writer and `IDbTarget`:
  - `AssertIdempotentAsync` — replays an interaction and verifies row count is stable within an eventual-consistency window
  - `AssertNoPartialWritesOnErrorAsync` — asserts a failing interaction leaves no row-count delta
- `AspireEFCoreInvariantException` — thrown on composite invariant violations
- `DbSnapshotInteraction` — addressed interaction record capturing `DbContext` state at a step boundary
- `AspireInteractionSequenceBuilder` — fluent builder mixing `Http`, `Message`, and `DbSnapshot` steps into a `Strategy<IReadOnlyList<IAddressedInteraction>>` for end-to-end Aspire scenarios

---

## [0.26.0] — 2026-04-27

### Added

**AspNetCore.EFCore** (`Conjecture.AspNetCore.EFCore`) — new package
- `AspNetCoreDbTarget<TContext>` — composite `IDbTarget` resolving a `DbContext` from an `IHost`'s service provider, with `ResetAsync` and `DisposeAsync` for per-property test isolation
- `AspNetCoreEFCoreInvariants` — composite asserter combining HTTP and EF Core targets:
  - `AssertNoPartialWritesOnErrorAsync` — verifies failed HTTP requests do not leave partial DB state
  - `AssertCascadeCorrectnessAsync` — asserts DELETE on a root entity cascades correctly across navigations
  - `MarkIdempotent(predicate)` + `AssertIdempotentAsync` — fluent endpoint-marking + repeat-request invariant
- `AspNetCoreEFCoreInvariantException` — thrown on composite invariant violations

**EFCore** (`Conjecture.EFCore`)
- `EntitySnapshot` + `EntitySnapshotter.CaptureAsync` / `Diff` — typed snapshot of entity counts and primary keys for before/after comparison
- `EntitySnapshotDiff.ToReport()` — human-readable per-type delta with added/removed key listing
- `IDbTargetExtensions.Resolve<TContext>()` — typed accessor over `IDbTarget`
- `DbInvariantException` — base exception for EF Core invariant assertion failures

### Removed

**Core** (`Conjecture.Core`)
- `Generate.Constant<T>(value)` — alias removed; use `Generate.Just<T>(value)` instead (#587)

---

## [0.25.0] — 2026-04-27

### Added

**EFCore** (`Conjecture.EFCore`) — new package
- `PropertyStrategyBuilder.Build(IProperty)` — maps an EF Core `IProperty` to a primitive `Strategy<object?>` honouring nullable, `MaxLength`, `Precision`/`Scale`, and `ValueGenerated` (returns CLR default so EF assigns on insert)
- `EntityStrategyBuilder` — `IModel`-driven entity-graph builder with `WithMaxDepth(depth)` (default 2, aligned with `Generate.Recursive()`), `WithoutNavigation<T>(expr)`, and `Build<TEntity>()`; required navigations beyond depth bound reuse the first generated parent, optional reference navigations are nulled, collection navigations are emitted empty, owned types are inlined
- `Generate.Entity<T>(DbContext, int maxDepth = 2)` and `Generate.Entity<T>(Func<DbContext>, int maxDepth = 2)` — extension-block strategy factories on `Generate`
- `RoundtripAsserter.AssertRoundtripAsync` and `AssertNoTrackingMatchesTrackedAsync` — save → reload-from-fresh-context → deep-compare via `IProperty.PropertyInfo!.GetValue` (no cross-context attach); `RoundtripAssertionException` on mismatch
- `MigrationHarness.AssertUpDownIdempotentAsync` — applies migrations to head, snapshots `sqlite_master`, runs `IMigrator.MigrateAsync(rollbackTarget)`, re-applies to head, and throws `MigrationAssertionException` if the schema diverges (SQLite-only in v1)
- Layer-1 interaction surface: `DbInteraction` record + `DbOpKind` enum (`Add`, `Update`, `Remove`, `SaveChanges`, `Query`); `IDbTarget` interface + `InMemoryDbTarget` and `SqliteDbTarget` (the latter `IAsyncDisposable`)
- `Generate.Db.{Add<T>, Update<T>, Remove<T>, SaveChanges, Sequence}` — `Strategy<DbInteraction>` builders, plus `Sequence` returning `Strategy<IReadOnlyList<DbInteraction>>` for `InteractionStateMachine<TState>`
- `DbInvariantExtensions.{AssertRoundtripAsync, AssertConcurrencyTokenRespectedAsync, AssertNoOrphansAsync, AssertNoTrackingMatchesTrackedAsync}` — fluent assertions on `IDbTarget` mirroring `HttpInvariantExtensions` (orphan walk uses EF metadata + `FindAsync`; no raw SQL in production)

---

## [0.24.0] — 2026-04-27

### Added

**Aspire** (`Conjecture.Aspire`) — new package
- `IAspireAppFixture` — fixture seam for managing the lifecycle of a `DistributedApplication`, including `StartAsync`, `ResetAsync`, `WaitForHealthyAsync`, `HealthCheckedResources`, `MaxRetryAttempts`, and `RetryDelay`
- `AspireSessionLifetimeHandler` — Microsoft Testing Platform session lifetime handler that starts the Aspire app once per test session and disposes it on completion
- `Interaction` record — HTTP interaction payload (`ResourceName`, `Method`, `Path`, `Body`) for driving generated calls against Aspire-hosted resources
- `AspireStateMachine<TState>` — abstract state machine base for stateful Aspire properties; expose `Commands`, `InitialState`, `Invariant`, `RunCommand`, plus `GetClient(resourceName)` to resolve resource HTTP clients
- `AspireProperty.RunAsync` — runner that drives an `AspireStateMachine<TState>` against an `IAspireAppFixture` under `ConjectureSettings`

**Core** (`Conjecture.Core`)
- `Generate.Constant<T>(value)` — strategy that always produces the same value

---

## [0.23.0] — 2026-04-26

### Added

**AspNetCore** (`Conjecture.AspNetCore`) — new package
- `Generate.AspNetCoreRequests(host, client)` — entry point for generating HTTP requests against a live ASP.NET Core host
- `AspNetCoreRequestBuilder` — fluent builder: `ValidRequestsOnly()`, `MalformedRequestsOnly()`, `ExcludeEndpoints(predicate)`, `WithSetup(delegate)`, `FromOpenApi(doc)`, `Build()`
- `DiscoveredEndpoint` record — endpoint metadata (display name, HTTP method, route pattern, parameters, content types, auth requirement)
- `EndpointParameter` record — per-parameter descriptor (name, CLR type, binding source, required flag)

### Changed

**Core** (`Conjecture.Core`)
- `GenForRegistry` renamed to `GenerateForRegistry` for consistency with the `Generate` API surface
- `GenerateForRegistry` gains `IsRegistered(Type)`, `Register(Type, factory, boxedStrategy)`, and `ResolveBoxed(Type)`

---

## [0.22.0] — 2026-04-26

### Added

**Grpc** (`Conjecture.Grpc`) — new package
- `GrpcRpcMode` enum — `Unary`, `ServerStream`, `ClientStream`, `Bidi` RPC modes
- `GrpcInteraction` record — gRPC interaction payload implementing `IInteraction`, carrying `ResourceName`, `FullMethodName`, `Mode`, `RequestMessages`, `Metadata`, and optional `Deadline`
- `GrpcResponse` record — result envelope with `Status` (`Grpc.Core.StatusCode`), `StatusDetail`, `ResponseMessages`, `ResponseHeaders`, and `Trailers`
- `IGrpcTarget` — call-invoker seam (`GetCallInvoker`) for supplying gRPC channels to the runner
- `GrpcChannelTarget` — `IGrpcTarget` + `IInteractionTarget` backed by a `GrpcChannel`; accepts either a `GrpcChannel` or an address string
- `HostGrpcTarget` — `IGrpcTarget` + `IInteractionTarget` backed by a `Microsoft.Extensions.Hosting.IHost`, for in-process server tests
- `GenerateGrpc.Unary`, `GenerateGrpc.ServerStream`, `GenerateGrpc.ClientStream`, `GenerateGrpc.BidiStream` — strategy factories for all four gRPC streaming modes
- `GrpcInvariantExtensions` — fluent response-assertion helpers: `AssertStatusOk()`, `AssertStatus(expected)`, `AssertNoUnknownStatus()`
- `GrpcInvariantException` — thrown by invariant extensions on status-code violations

---

## [0.21.0] — 2026-04-26

### Added

**Messaging** (`Conjecture.Messaging`) — new package
- `MessageInteraction` record (`Destination`, `Body`, `Headers`, `MessageId`, `CorrelationId`) implementing `IInteraction` — service-bus / message-queue payload type for property tests built on the v0.20 `Conjecture.Interactions` foundation
- `IMessageBusTarget : IInteractionTarget` — pull-based receive (`ReceiveAsync`) plus explicit `AcknowledgeAsync` / `RejectAsync(requeue:bool)` for redelivery and dead-letter property tests
- `InMemoryMessageBusTarget` — deterministic reference adapter backed by `Channel<MessageInteraction>` per destination, suitable for the bulk of user tests and self-tests
- `Generate.Messaging.Publish(destination, bodyStrategy)` and `Generate.Messaging.Consume(destination)` strategy verbs (plus a full-control `Publish` overload accepting headers and correlation-id strategies). `MessageId` is generated deterministically so failed runs reproduce byte-for-byte under a fixed seed
- Body strategies compose with `Generate.FromProtobuf<T>()` and `Generate.FromJsonSchema(...)` from v0.21

**Messaging.AzureServiceBus** (`Conjecture.Messaging.AzureServiceBus`) — new package
- `AzureServiceBusTarget : IMessageBusTarget, IAsyncDisposable` over `Azure.Messaging.ServiceBus`
- `AzureServiceBusTarget.Connect(string connectionString)` static factory plus a `(IServiceBusClientAdapter client)` constructor for fakes
- Public seam interfaces (`IServiceBusClientAdapter`, `IServiceBusSender`, `IServiceBusReceiver`, `IServiceBusMessageAdapter`, `IServiceBusReceivedMessageAdapter`) so users can substitute custom client wrappers (e.g. `DefaultAzureCredential`-backed clients) without taking a dependency on the SDK in their tests
- `RejectAsync(requeue:true)` → `AbandonMessageAsync`; `requeue:false` → `DeadLetterMessageAsync`. Headers round-trip via `ServiceBusMessage.ApplicationProperties` (non-string values converted via `Convert.ToString(InvariantCulture)` rather than silently dropped)

**Messaging.RabbitMq** (`Conjecture.Messaging.RabbitMq`) — new package
- `RabbitMqTarget : IMessageBusTarget, IAsyncDisposable` over `RabbitMQ.Client` 7.x
- `RabbitMqTarget.Connect(connectionString)` (sync) and `RabbitMqTarget.ConnectAsync(connectionString, ct)` (async — preferred) static factories plus a `(IRabbitMqConnectionAdapter)` constructor for fakes
- Public seam interfaces (`IRabbitMqConnectionAdapter`, `IRabbitMqChannelAdapter`, `IRabbitMqMessageAdapter`, `IRabbitMqReceivedMessageAdapter`)
- `RejectAsync(requeue:true)` → `BasicNackAsync(requeue:true)`; `requeue:false` → `BasicNackAsync(requeue:false)`. Header values UTF-8 encode/decode through the AMQP `byte[]` wire form

**Mcp** (`Conjecture.Mcp`)
- `scaffold-messaging-property-test` MCP tool — generates a ready-to-fill `Property.ForAll(target, Generate.Messaging.Publish(...), ...)` test class. Parameterized by `framework` (xunit | xunit-v3 | nunit | mstest), `broker` (inmemory | azureservicebus | rabbitmq), `destination`, and `bodyType` (bytes | protobuf | jsonschema)

**Docs**
- Explanation: `messaging-property-testing.md` — concepts, shrinking semantics, two-tier test plan
- How-to: `test-messaging-with-inmemory.md`, `test-messaging-with-azure-service-bus.md`, `test-messaging-with-rabbitmq.md` (xUnit v3 examples)
- ADR 0061 — `Conjecture.Messaging` package design (pull vs push, body shape, shrinking pairs, per-adapter test strategy)

---

## [0.20.0] — 2026-04-25

### Added

**JsonSchema** (`Conjecture.JsonSchema`) — new package
- `Generate.FromJsonSchema(string jsonSchemaText)` — generate `JsonElement` values matching a JSON Schema document (text)
- `Generate.FromJsonSchema(JsonElement root)` — generate `JsonElement` values matching a parsed JSON Schema root
- `Generate.FromJsonSchema(FileInfo schemaFile)` — generate `JsonElement` values from a JSON Schema file
- `JsonSchemaType` enum — `None`, `Null`, `Boolean`, `Integer`, `Number`, `String`, `Array`, `Object`, `Any`

**OpenApi** (`Conjecture.OpenApi`) — new package
- `Generate.FromOpenApi(string filePath)` / `(FileInfo file)` / `(Uri url)` — load an OpenAPI document and obtain an `OpenApiDocument` for strategy construction
- `OpenApiDocument.PathParameter(method, path, name, maxDepth)` — strategy for a path parameter on a given operation
- `OpenApiDocument.QueryParameter(method, path, name, maxDepth)` — strategy for a query parameter on a given operation
- `OpenApiDocument.RequestBody(method, path, maxDepth)` — strategy for the request body on a given operation
- `OpenApiDocument.ResponseBody(method, path, statusCode, maxDepth)` — strategy for the response body on a given operation+status

**Protobuf** (`Conjecture.Protobuf`) — new package
- `Generate.FromProtobuf<T>(int maxDepth = 5)` — generate `JsonElement` values matching a Protobuf message type
- `Generate.FromProtobuf(MessageDescriptor descriptor, int maxDepth = 5)` — generate `JsonElement` values from a runtime Protobuf descriptor
- `ProtobufFieldStrategy` — public strategy backing Protobuf-driven generation

**Regex** (`Conjecture.Regex`)
- `Generate.Date()`, `Generate.Time()`, `Generate.Ipv4()`, `Generate.Ipv6()` — new built-in pattern strategies for ISO date, ISO time, IPv4, and IPv6 strings
- `KnownRegex.Date`, `KnownRegex.Time`, `KnownRegex.Ipv4`, `KnownRegex.Ipv6` — corresponding `Regex` accessors (source-generated via `[GeneratedRegex]`)

### Changed

**Regex** (`Conjecture.Regex`) — **binary breaking change**
- `KnownRegex.CreditCard`, `Email`, `IsoDate`, `Url`, `Uuid` migrated from `static readonly Regex` fields to `static partial Regex` properties decorated with `[GeneratedRegex]`. Source-level call sites are unchanged; existing compiled consumers must be recompiled.

---

## [0.19.0] — 2026-04-25

### Added

**Http** (`Conjecture.Http`) — new package
- `HttpInteraction` record — represents a single HTTP request interaction (resource name, method, path, optional body, optional headers)
- `IHttpTarget` interface — resolves an `HttpClient` by resource name for executing interactions
- `HostHttpTarget` — `IHttpTarget` implementation backed by `Microsoft.Extensions.Hosting.IHost` and a pre-configured `HttpClient`; implements `IAsyncDisposable`
- `HttpStrategyBuilder` — fluent builder for `Strategy<HttpInteraction>`; verbs `Get`, `Post`, `Put`, `Delete`, `Patch`; modifiers `WithHeaders`, `WithResource`, `WithBodyStrategy`
- `HttpGenerateExtensions` — surfaces `Generate.Http(resourceName)` via a C# 14 `extension(Generate)` block; a single `using Conjecture.Http;` is enough
- `HttpInvariantExtensions` — assertion helpers on `Task<HttpResponseMessage>`: `AssertNot5xx`, `Assert4xx`, `AssertProblemDetailsShape`; `Response(target)` extension on `HttpInteraction` to dispatch a request and return the response
- `HttpInvariantException` — exception type thrown when an HTTP invariant is violated

**Interactions** (`Conjecture.Interactions`) — new package
- `IInteraction` — base marker interface for interaction commands
- `IAddressedInteraction` — interaction with a `ResourceName` for routing to a named target
- `IInteractionTarget` — target that executes interactions and returns `object?` results
- `CompositeInteractionTarget` — fan-out target that routes `IAddressedInteraction` commands to named sub-targets by resource name
- `InteractionStateMachine<TState>` — abstract base for model-based interaction testing; implement `InitialState`, `Commands`, `RunCommand`, and `Invariant`
- `Property.ForAll<T>(IInteractionTarget, Strategy<T>, assertion)` — runs a property test against an interaction target with a generated value
- `Property.ForAll<TState>(IInteractionTarget, InteractionStateMachine<TState>)` — runs a full state-machine property test against an interaction target

---

## [0.18.0] — 2026-04-23

### Added

**Time** (`Conjecture.Time`)
- `Generate.IanaZoneIds(bool preferDst = false)` — generates IANA time zone ID strings, with optional DST-aware bias
- `Generate.WindowsZoneIds()` — generates Windows registry time zone ID strings
- `Generate.TimeZone(bool preferDst = false)` — generates `TimeZoneInfo` instances (unified replacement for the separate IANA/Windows overloads)
- `Generate.AdvancingClocks(TimeSpan maxJump)` — generates `FakeTimeProvider` instances whose clock advances monotonically on each read
- `Generate.ClockWithAdvances(int advanceCount, TimeSpan maxJump, bool allowBackward = false)` — generates a `FakeTimeProvider` paired with its list of time advances
- `Generate.RecurringEvents(Func<DateTimeOffset, DateTimeOffset?> nextOccurrence, TimeZoneInfo zone, TimeSpan window)` — generates `RecurringEventSample` values: a DST-aware window of event occurrences for testing scheduling logic
- `RecurringEventSample` — record holding `WindowStart`, `WindowEnd`, `Occurrences`, `Zone`, and `NextOccurrence` for a recurring event sample
- `.NearDstTransition()` on `Strategy<RecurringEventSample>` — biases the recurring event window toward DST transition boundaries
- `Strategy<DateTimeOffset>.WithPrecision(TimeSpan precision)` — truncates generated offsets to the given precision
- `Strategy<DateTimeOffset>.WithStrippedOffset()` — returns both the original and a zero-UTC-offset copy as a named tuple
- `Strategy<DateTime>.WithKinds()` — annotates each generated `DateTime` with its `DateTimeKind` as a named tuple
- `Strategy<DateOnly>.NearMonthBoundary()` — biases generated dates toward the first and last days of months
- `Strategy<DateOnly>.NearLeapDay()` — biases generated dates toward February 29 on leap years
- `Strategy<TimeOnly>.NearMidnight()` / `.NearNoon()` / `.NearEndOfDay()` — bias time-of-day generation toward common boundary values

---

## [0.17.0] — 2026-04-23

### Added

**Money** (`Conjecture.Money`) — new package
- `Generate.Decimal(decimal min, decimal max, int? scale = null)` — scaled decimal generation within a range; defaults to 6 decimal places when scale is omitted
- `Generate.Iso4217Codes()` — samples uniformly from all 141 active ISO 4217 alphabetic currency codes (embedded snapshot; excludes withdrawn codes)
- `Generate.Amounts(string currencyCode, decimal min = 0m, decimal max = 10_000m)` — generates decimal amounts scaled to the currency's ISO 4217 minor-unit decimal places (0 for JPY, 2 for USD, 3 for BHD); throws `ArgumentException` for unknown codes
- `Generate.RoundingModes()` — samples uniformly from all `System.MidpointRounding` enum values

---

## [0.16.0] — 2026-04-22

### Added

**Core** (`Conjecture.Core`)
- `Generate.OneOf<T>(params ReadOnlySpan<Strategy<T>> strategies)` — span overload that avoids heap-array allocation at inline call sites (`Generate.OneOf(a, b, c)`). The array overload is retained for ABI compatibility and collection-passed call sites.

### Changed

**Time** (`Conjecture.Time`)
- `TimeGenerate.TimeZones()` and `TimeGenerate.ClockSet(...)` are now exposed as `Generate.TimeZones()` and `Generate.ClockSet(...)` via a C# 14 `extension(Generate)` block on `TimeGenerateExtensions`. A single `using Conjecture.Time;` is enough — no separate `TimeGenerate` class needed. The `TimeGenerate` static class has been removed (breaking change, pre-1.0).

---

## [0.15.0] — 2026-04-22

### Added

**Regex** (`Conjecture.Regex`)
- `Generate.ReDoSHunter(string pattern, int maxMatchMs = 5)` — adversarial string generation that targets catastrophic backtracking; biases repetition draws toward maximum counts and appends a non-matching suffix to force anchor failure
- `Generate.ReDoSHunter(Regex regex, int maxMatchMs = 5)` — same, accepting a pre-compiled `Regex` instance; `RegexOptions.NonBacktracking` falls back to `Generate.Matching` with diagnostic label `"redos:non-backtracking"`

---

## [0.14.0] — 2026-04-21

### Added

**Core** (`Conjecture.Core`)
- `Generate.For<T>()` / `Generate.For<T>(configure)` — automatic data generation for arbitrary types, with optional per-property strategy overrides via `ForConfiguration<T>`
- `ForConfiguration<T>` — fluent builder for overriding individual property strategies and reading them back by name
- `GenForRegistry` — low-level registry for custom type factories and per-property overrides (`Register`, `RegisterOverride`, `ResolveWithOverrides`)
- `Generate.Guids()` — generates random `Guid` values
- `Generate.Decimals()` / `Generate.Decimals(min, max)` — generates `decimal` values
- `Generate.DateTimes()` / `Generate.DateTimes(min, max)` — generates `DateTime` values
- `Generate.Chars()` — generates random `char` values
- `[GenRange]` attribute — annotate numeric properties with a `[min, max]` range hint for automatic generation
- `[GenStringLength]` attribute — annotate string properties with `minLength`/`maxLength` hints
- `[GenRegex]` attribute — annotate string properties with a regex pattern hint
- `[GenMaxDepth]` attribute — annotate recursive types with a max recursion depth hint

### Changed

**Regex** (`Conjecture.Regex`)
- `Matching`, `NotMatching`, `Email`, `NotEmail`, `Url`, `NotUrl`, `Uuid`, `NotUuid`, `IsoDate`, `NotIsoDate`, `CreditCard`, `NotCreditCard` now surface on `Generate.*` via a C# 14 `extension(Generate)` block on `Conjecture.Core.RegexGenerateExtensions`. A single `using Conjecture.Core;` is enough to see them — no `using Conjecture.Regex;` required. The `RegexGenerate` static factory has been removed (pre-release, no forwarder)
- `KnownRegex` — static class exposing compiled `Regex` instances for common patterns (`Email`, `Url`, `Uuid`, `IsoDate`, `CreditCard`)
- `RegexGenOptions` / `UnicodeCoverage` — options type for controlling Unicode coverage (`Ascii` vs `Full`) in regex-based generators

---

## [0.13.0] — 2026-04-19

### Added

**TestingPlatform** (`Conjecture.TestingPlatform`)
- `ConjectureTestingPlatformExtensions` — extension class for integrating Conjecture with Microsoft Testing Platform (MTP)
- `RegisterConjectureFramework` extension on `ITestApplicationBuilder` — registers the Conjecture test framework with MTP in one call
- `AddExtensions` helper — wires up MTP extensions from command-line args

---

## [0.12.0] — 2026-04-19

### Added

**Core** (`Conjecture.Core`)
- `SelectManyDirectStrategy<TSource, TResult>` — internal zero-alloc `SelectMany` path; accepts `Func<TSource, ConjectureData, TResult>` directly, eliminating the per-`Generate` wrapper allocation (~32 B/call saving)
- Internal `StrategyExtensions.SelectMany` overload routing to `SelectManyDirectStrategy` for hot-path composition

### Changed

**Core** (`Conjecture.Core`)
- `RecursiveStrategy<T>` pre-builds the full depth-level array at construction time, eliminating per-`Generate` `DepthLimitedStrategy` heap allocations
- `WhereStrategy` now rolls back rejected IR nodes (`data.TruncateNodes`) to prevent unbounded node accumulation on filtered-out values

---

## [0.11.0] — 2026-04-18

### Added

**FSharp** (new package `Conjecture.FSharp`)
- `Gen<'a>` type — F#-native generator wrapping `Strategy<T>`
- `Gen` module with primitives: `int`, `bool`, `float`, `string`, `guid`, `byte`, `char`; combinators: `list`, `option`, `result`, `set`, `seq`, `tuple2`
- `Gen.auto<'a>` — automatic generator derivation via `FSharp.Reflection` for records, discriminated unions, and tuples
- `gen { }` computation expression (`GenBuilder`) for monadic generator composition
- `PropertyRunner` — runs property tests from F# using `ConjectureSettings`
- `FSharpFormatter` — pretty-printer for F# union and record values in failure output

**FSharp.Expecto** (new package `Conjecture.FSharp.Expecto`)
- `property` combinator — integrates Conjecture property tests with the Expecto test framework

**Generators** (`Conjecture.Generators`)
- `HierarchyTypeModel` and `HierarchyTypeModelExtractor` — model for sealed class hierarchies
- `HierarchyStrategyEmitter` — emits `Generate.OneOf(…)` strategies for abstract base types with sealed concrete subtypes
- Hierarchy pipeline wired into `ArbitraryGenerator` for auto-generation of sealed hierarchies

**Analyzers** (`Conjecture.Analyzers`)
- CON205 — warns when a concrete subtype of a sealed hierarchy is missing an `[Arbitrary]` attribute

**MCP** (`Conjecture.Mcp`)
- `suggest-strategy-for-sealed-hierarchy` tool — suggests generation strategies for sealed class hierarchies

---

## [0.10.0] — 2026-04-17

### Added

**Core** (`Conjecture.Core`)
- `ConjectureObservability` — static `ActivitySource` and `Meter` singletons (name `"Conjecture.Core"`) for OpenTelemetry trace and metrics integration; zero overhead when no listener is attached
- `PartialConstructorContext` — context carrier for partial constructor generation; exposes `Current` and `Use()` for strategy emitters
- `ConjectureSettings.TestName` — optional test method name; populated by framework adapters to tag the `test.name` attribute on the `PropertyTest` trace span
- `ConjectureSettings.TestClassName` — optional test class name; populated by framework adapters to tag the `test.class.name` attribute on the `PropertyTest` trace span
- OTel Activity spans in `TestRunner`: `PropertyTest` (root), `PropertyTest.Generation`, `PropertyTest.Shrinking`, `PropertyTest.Targeting` — each with relevant tags (seed, examples, reductions, labels, etc.)
- OTel metrics instruments: `conjecture.property.examples_total`, `conjecture.property.failures_total`, `conjecture.property.duration_seconds`, `conjecture.generation.rejections_total`, `conjecture.shrink.passes_total`, `conjecture.shrink.reductions_total`, `conjecture.targeting.best_score`, `conjecture.database.replays_total`, `conjecture.database.saves_total`

**LinqPad** (`Conjecture.LinqPad`)
- `StrategyCustomMemberProvider<T>` — LINQPad custom member provider for `Strategy<T>`; surfaces generated sample values in the LINQPad results panel
- `StrategyLinqPadExtensions.ShrinkTraceHtml<T>` — extension that renders a shrink trace as an HTML object for LINQPad output

---

## [0.9.0] — 2026-04-15

### Added

**TestingPlatform** (new package `Conjecture.TestingPlatform`)
- Native Microsoft Testing Platform adapter — test project runs as a self-contained executable (`OutputType=Exe`), no framework runner required
- `[Property]` attribute with full settings surface: `MaxExamples`, `Seed`, `UseDatabase`, `MaxStrategyRejections`, `DeadlineMs`, `Targeting`, `TargetingProportion`, `ExportReproOnFailure`, `ReproOutputPath`
- CLI options `--conjecture-seed` and `--conjecture-max-examples` to override settings globally at run time
- `ITrxReportCapability` support for TRX report generation via `dotnet test --report-trx`

### Changed

- `Conjecture.Interactive`: all output switched from HTML/SVG to plain text
- `SvgHistogram` renamed to `TextHistogram`
- `ShrinkTraceResult<T>.Html` renamed to `.Text`

### Removed

- `ConjectureKernelExtension` — Polyglot Notebooks auto-load (`Microsoft.DotNet.Interactive` deprecated)
- `Microsoft.DotNet.Interactive` dependency removed from `Conjecture.Interactive`

---

## [0.8.0] — 2026-04-14

### Added

**Analyzers** (new package `Conjecture.Analyzers`, bundled into `Conjecture.Core.nupkg`)
- CON107: Non-deterministic operation inside `[Property]` (`Guid.NewGuid()`, `DateTime.Now`, `Random`, etc.)
- CON108: `Assume.That` condition always true given built-in strategy constraint (`PositiveInts`, `NegativeInts`, `NonNegativeInts`)
- CON109: Missing strategy for `[Property]` parameter type
- CON110: Async `[Property]` method contains no `await`
- CON111: `Target.Maximize`/`Target.Minimize` outside `[Property]` method
- CJ0050: Suggest named extension property (`.Positive`, `.NonEmpty`) instead of equivalent `.Where()` — with code fix

**Time** (new package `Conjecture.Time`)
- `TimeGenerate.TimeZones()` — strategy over system time zones, shrinks toward UTC
- `TimeGenerate.ClockSet(nodeCount, maxSkew)` — generates an array of `FakeTimeProvider` instances with clock skew
- `TimeProviderArbitrary` — `[Arbitrary]` auto-provider for `TimeProvider` parameters
- `DateTimeOffsetExtensions`: `.NearMidnight()`, `.NearLeapYear()`, `.NearEpoch()`, `.NearDstTransition(zone?)`

**Interactive** (new package `Conjecture.Interactive`)
- `Strategy<T>.Preview(count, seed)` — quick-look HTML table of sample values
- `Strategy<T>.SampleTable(count, seed)` — indexed HTML sample table
- `Strategy<T>.Histogram(sampleSize, bucketCount, seed)` — SVG histogram of distribution
- `Strategy<T>.ShrinkTrace(seed, failingProperty)` — step-by-step shrink trace
- `ConjectureKernelExtension` — Polyglot Notebooks auto-load

**Core**
- `IPropertyTest` and `IReproductionExport` interfaces for framework-agnostic attribute introspection
- `ConjectureSettings.From(IPropertyTest, ILogger?)` factory for constructing settings from attribute data
- `ConjectureStrategyRegistrar` for plugging in custom strategy resolution
- `Generate.FromBytes<T>(ReadOnlySpan<byte>)` — deterministic replay from a fixed byte buffer
- `Generate.DateTimeOffsets()` / `Generate.DateTimeOffsets(min, max)`
- `Generate.TimeSpans()` / `Generate.TimeSpans(min, max)`
- `Generate.DateOnlyValues()` / `Generate.DateOnlyValues(min, max)`
- `Generate.TimeOnlyValues()` / `Generate.TimeOnlyValues(min, max)`

**Tool**
- `PlanRunner` resolves `IStrategyProvider<T>` via reflection for arbitrary types in plan steps

---

## [0.7.0] — 2026-04-11

### Added

**Core**
- `DataGen` static class with `Sample<T>`, `SampleOne<T>`, and `Stream<T>` for generating data outside of property tests
- `IOutputFormatter` interface with `Name` and `WriteAsync<T>` for pluggable output serialisation
- `ConjectureSettings.ExportReproOnFailure` and `ReproOutputPath` for saving reproduction scripts on failure
- `StrategyExtensionProperties` extension properties on `Strategy<int>`, `Strategy<string>`, and `Strategy<List<T>>`: `.Positive`, `.Negative`, `.NonZero`, `.NonEmpty`
- `|` operator on `Strategy<T>` via `StrategyExtensionProperties` for strategy union
- `Generate.Identifiers(...)`, `Generate.NumericStrings(...)`, `Generate.VersionStrings(...)` string generators

**Formatters** (new package `Conjecture.Formatters`)
- `JsonOutputFormatter` — serialises generated data as a JSON array
- `JsonLinesOutputFormatter` — serialises generated data as newline-delimited JSON

**Tool** (new package `Conjecture.Tool`)
- `AssemblyLoader` — discovers `IStrategyProvider` types in an assembly
- `GenerateCommand.ExecuteAsync(...)` — CLI entry point for ad-hoc data generation
- `Plan` sub-namespace: `GenerationPlan`, `PlanStep`, `OutputConfig`, `PlanRunner`, `PlanResult`, `RefExpression`, `RefResolver`, `PlanException` for YAML-driven multi-step generation plans

**Xunit**
- `PropertyAttribute.ExportReproOnFailure` and `ReproOutputPath` for per-test repro export configuration

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

[0.9.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.9.0
[0.8.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.8.0
[0.7.0]: https://github.com/kommundsen/Conjecture/releases/tag/v0.7.0
[0.6.0-alpha.1]: https://github.com/kommundsen/Conjecture/releases/tag/v0.6.0-alpha.1
