# Phase 1 Implementation Plan: Extended Strategies, Formatters, Settings & Database

## Context

Phase 0 delivered a working end-to-end property test engine with random generation, basic strategies (bool, integer, bytes), LINQ combinators, `[Property]` attribute, basic shrinking (4 passes), and primitive failure reporting — a faithful Conjecture engine port (ADR-0008) with xUnit v3 integration (ADR-0007). Phase 1 extends the library to be practically useful: rich strategy library (floats, strings, collections, choice), full formatter pipeline, extended settings with health checks, and an SQLite example database for regression prevention. The API remains 0.x with no stability guarantee (ADR-0004), targeting .NET 10 (ADR-0006).

**Deferred to Phase 2+:** Source generators (ADR-0010), Roslyn analyzers (ADR-0023), stateful testing (ADR-0015), advanced shrinker passes (ADR-0021), F# API (ADR-0013), NUnit/MSTest integration, imperative composition via `IGeneratorContext` (ADR-0019).

**End-state goal:** A user can write:
```csharp
[Property]
public void Reverse_twice_is_identity(List<int> xs) =>
    Assert.Equal(xs, xs.AsEnumerable().Reverse().Reverse().ToList());
```
and get rich failure output with formatted collections, automatic regression storage, and configurable settings.

## Dependency Graph

```
Gen.Just/OneOf/SampledFrom/Enums ──────────────────────────────────┐
                                                                    │
FloatingPointStrategy ──┐                                           │
                        ├──> StringStrategy ──> CollectionStrategies ├──> ParameterStrategyResolver
                        │                                           │
NullableStrategy / TupleStrategies ─────────────────────────────────┘

WithLabel combinator ──────────────────────────────────────────────────> FormatterRegistry
                                                                               │
IStrategyFormatter<T> ──> FormatterRegistry (Holder<T>) ──> CounterexampleFormatter (enhanced)
                                                                               │
ConjectureSettings (extended) ──> SettingsResolver ──> TestRunner (modified) ──┤
                                                            │                  │
ExampleDatabase (SQLite) ───────────────────────────────────┘                  │
                                                                               v
                                                                   PropertyTestCaseRunner (enhanced)
```

## Pre-requisites

- [ ] Verify `v0.1.0-alpha` git tag exists for MinVer (ADR-0001)
- [ ] Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` to `Conjecture.Core.csproj` and `Conjecture.Xunit.csproj`; create `PublicAPI.Shipped.txt` (empty) and `PublicAPI.Unshipped.txt` (populated with Phase 0 public API) in each project root (ADR-0002)
- [ ] Configure `Microsoft.DotNet.ApiCompat` baseline from Phase 0 output (ADR-0003)
- [ ] Confirm `LICENSE` file exists in repo root; add MPL-2.0 file headers to any code ported from Python Hypothesis (ADR-0005, ADR-0026)
- [ ] Add `Microsoft.Data.Sqlite` to `Directory.Packages.props` and reference in `Conjecture.Core.csproj` (ADR-0012)
- [ ] `/scaffold module Conjecture.Core Formatting` -- Create Formatting directory
- [ ] `/scaffold module Conjecture.Core Internal/Database` -- Create Database directory
- [ ] `/decision` -- IEEE 754 floating-point strategy design (extends ADR-0011 to `IFloatingPoint<T>`)

## TDD Execution Plan

Each cycle: `/test` (Red) then `/implement` (Green). 10 sub-phases.

---

### 1.1 Simple Choice Strategies

#### Cycle 1.1.1 -- Gen.Just (constant strategy)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/JustStrategyTests.cs`
  - `Gen.Just(42)` always returns 42, `Gen.Just("hello")` always returns `"hello"`, draws zero IR nodes from ConjectureData, works with Select combinator
- [x] `/implement` -- `src/Conjecture.Core/Strategies/JustStrategy.cs` + add `Gen.Just<T>(T value)` to `Gen.cs`
  - Update `PublicAPI.Unshipped.txt` (ADR-0002)

#### Cycle 1.1.2 -- Gen.OneOf (union/choice strategy, ADR-0018)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/OneOfStrategyTests.cs`
  - `Gen.OneOf(Gen.Just(1), Gen.Just(2))` returns 1 or 2, distribution covers all branches over many draws, single-strategy OneOf delegates directly, empty array throws `ArgumentException`
- [x] `/implement` -- `src/Conjecture.Core/Strategies/OneOfStrategy.cs` + add `Gen.OneOf<T>(params Strategy<T>[])` to `Gen.cs`
  - Uses `DrawInteger(0, n-1)` to pick index, then delegates to chosen strategy
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.1.3 -- Gen.SampledFrom (pick from fixed set)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/SampledFromStrategyTests.cs`
  - `Gen.SampledFrom([1, 2, 3])` returns only values from set, empty throws `ArgumentException`, single-element always returns that element, deterministic with seed
- [x] `/implement` -- `src/Conjecture.Core/Strategies/SampledFromStrategy.cs` + add `Gen.SampledFrom<T>(IReadOnlyList<T>)` to `Gen.cs`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.1.4 -- Gen.Enums (enum strategy)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/EnumStrategyTests.cs`
  - `Gen.Enums<DayOfWeek>()` returns valid DayOfWeek values, covers all members over many draws, works with custom enums
- [x] `/implement` -- `src/Conjecture.Core/Strategies/EnumStrategy.cs` + add `Gen.Enums<T>() where T : struct, Enum` to `Gen.cs`
  - Pre-computes `Enum.GetValues<T>()` at construction (values cached, no per-draw reflection)
  - Update `PublicAPI.Unshipped.txt`

---

### 1.2 Floating Point Strategy (ADR-0011 extension)

#### Cycle 1.2.1 -- FloatingPointStrategy core
- [x] `/test` -- `src/Conjecture.Tests/Strategies/FloatingPointStrategyTests.cs`
  - `Gen.Doubles()` generates values in full double range, `Gen.Floats()` generates float values, deterministic with seed, includes both positive and negative values
- [x] `/implement` -- `src/Conjecture.Core/Strategies/FloatingPointStrategy.cs`
  - `class FloatingPointStrategy<T> : Strategy<T> where T : IBinaryFloatingPointIeee754<T>`
  - Draws raw bits via `DrawInteger` (64-bit for double, 32-bit for float), reinterprets as floating point
  - Add `Gen.Doubles()`, `Gen.Floats()` to `Gen.cs`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.2.2 -- Floating point range constraints
- [x] `/test` -- `src/Conjecture.Tests/Strategies/FloatingPointRangeTests.cs`
  - `Gen.Doubles(0.0, 1.0)` stays in [0,1], `Gen.Floats(-1f, 1f)` stays in [-1,1], min==max returns constant, min>max throws `ArgumentException`
- [x] `/implement` -- Add range overloads `Gen.Doubles(min, max)`, `Gen.Floats(min, max)` to `Gen.cs`; extend `FloatingPointStrategy<T>` with range support
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.2.3 -- Floating point special values (NaN, Infinity, subnormals)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/FloatingPointSpecialValuesTests.cs`
  - Unbounded `Gen.Doubles()` can produce NaN, +Infinity, -Infinity over sufficient draws; `Gen.Doubles(0.0, 1.0)` never produces NaN/Infinity; subnormals are representable
- [x] `/implement` -- Modify `FloatingPointStrategy<T>` to bias toward special IEEE 754 values for unbounded; range-bounded excludes non-finite

---

### 1.3 String Strategy

Strings compose from integer draws (length + char codes) — no new `IRNodeKind` needed. Existing shrinker passes work automatically on the underlying integer nodes.

#### Cycle 1.3.1 -- StringStrategy core (Gen.Strings / Gen.Text)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/StringStrategyTests.cs`
  - `Gen.Strings()` produces strings, deterministic with seed, default charset is printable ASCII, `Gen.Strings(minLength: 5, maxLength: 10)` respects bounds, empty string possible when minLength=0
- [x] `/implement` -- `src/Conjecture.Core/Strategies/StringStrategy.cs` + add `Gen.Strings()` and `Gen.Text()` to `Gen.cs`
  - `Gen.Text()` is alias for `Gen.Strings()` (Python Hypothesis convention)
  - Internally: `DrawInteger` for length, `DrawInteger` per character from charset range
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.3.2 -- String charset control
- [x] `/test` -- `src/Conjecture.Tests/Strategies/StringCharsetTests.cs`
  - `Gen.Strings(alphabet: "abc")` only produces strings from {a, b, c}, `Gen.Strings(minCodepoint, maxCodepoint)` respects codepoint range, unicode-safe generation
- [x] `/implement` -- Extend `StringStrategy` with charset parameters; add overloads to `Gen.cs`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.3.3 -- String shrinking quality
- [x] `/test` -- `src/Conjecture.Tests/Strategies/StringShrinkingTests.cs`
  - Failing string property shrinks toward shorter strings, shrinks toward earlier alphabet characters (e.g. 'a'), shrunk string still satisfies the failure condition
- [x] `/implement` -- (Likely green-on-write; shrinking is automatic via integer nodes. If not, adjust `StringStrategy` draw ordering to improve shrink quality)

---

### 1.4 Nullable and Tuple Strategies

#### Cycle 1.4.1 -- Gen.Nullable / .OrNull() combinator
- [x] `/test` -- `src/Conjecture.Tests/Strategies/NullableStrategyTests.cs`
  - `Gen.Nullable(Gen.Integers<int>())` produces both null and non-null, `.OrNull()` extension produces both null and non-null, non-null values come from inner strategy, null probability ~10%
- [x] `/implement` -- `src/Conjecture.Core/Strategies/NullableStrategy.cs` + `Gen.Nullable<T>(Strategy<T>)` in `Gen.cs` + `.OrNull()` in `StrategyExtensions.cs`
  - Internally: `DrawBoolean` to decide null vs non-null, delegate to inner strategy
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.4.2 -- Gen.Tuples (2-element)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/TupleStrategyTests.cs`
  - `Gen.Tuples(Gen.Integers<int>(), Gen.Booleans())` produces `(int, bool)` tuples, both components vary, deterministic with seed
- [x] `/implement` -- Add `Gen.Tuples<T1,T2>(Strategy<T1>, Strategy<T2>)` to `Gen.cs`
  - Delegates to existing `Zip` combinator internally
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.4.3 -- Gen.Tuples (3 and 4 element)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/TupleStrategy3And4Tests.cs`
  - `Gen.Tuples(s1, s2, s3)` produces 3-tuples, `Gen.Tuples(s1, s2, s3, s4)` produces 4-tuples, all components vary
- [x] `/implement` -- Add 3- and 4-arg `Gen.Tuples` overloads to `Gen.cs`
  - Update `PublicAPI.Unshipped.txt`

---

### 1.5 Collection Strategies

#### Cycle 1.5.1 -- Gen.Lists (ADR-0018)
- [x] `/test` -- `src/Conjecture.Tests/Strategies/ListStrategyTests.cs`
  - `Gen.Lists(Gen.Integers<int>())` produces `List<int>`, default size varies 0–~100, `Gen.Lists(s, minSize: 3, maxSize: 5)` respects bounds, empty list possible when minSize=0, deterministic with seed
- [x] `/implement` -- `src/Conjecture.Core/Strategies/ListStrategy.cs` + `Gen.Lists<T>(Strategy<T>, int minSize = 0, int maxSize = 100)` in `Gen.cs`
  - `DrawInteger` for size, then draw N elements via inner strategy
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.5.2 -- Gen.Sets
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/SetStrategyTests.cs`
  - `Gen.Sets(Gen.Integers<int>(0, 100))` produces `IReadOnlySet<int>` with unique elements, respects minSize/maxSize, handles rejection when inner strategy can't produce enough unique values
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/SetStrategy.cs` + `Gen.Sets<T>` in `Gen.cs`
  - Draws elements and deduplicates; uses rejection budget for uniqueness (ADR-0020)
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.5.3 -- Gen.Dictionaries
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/DictionaryStrategyTests.cs`
  - `Gen.Dictionaries(Gen.Integers<int>(0, 100), Gen.Strings())` produces dictionaries, keys are unique, respects minSize/maxSize
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/DictionaryStrategy.cs` + `Gen.Dictionaries<TKey,TValue>` in `Gen.cs`
  - Draws key-value pairs; deduplicates by key with rejection budget (ADR-0020)
  - Update `PublicAPI.Unshipped.txt`

---

### 1.6 WithLabel Combinator (ADR-0018)

#### Cycle 1.6.1 -- .WithLabel() extension
- [ ] `/test` -- `src/Conjecture.Tests/Strategies/WithLabelTests.cs`
  - `.WithLabel("age")` annotates strategy, label is accessible on strategy instance, generation still works correctly, labeled strategy produces same values as unlabeled
- [ ] `/implement` -- `src/Conjecture.Core/Strategies/LabeledStrategy.cs` + add `WithLabel<T>(this Strategy<T>, string)` to `StrategyExtensions.cs`
  - Add `string? Label` property to `Strategy<T>` base class (nullable, default null)
  - Update `PublicAPI.Unshipped.txt`

---

### 1.7 Formatter Pipeline (ADR-0022, ADR-0014)

Phase 0 scope: `CounterexampleFormatter` with `ToString()`. Phase 1: full `IStrategyFormatter<T>` / `FormatterRegistry` with C#-like literal output.

#### Cycle 1.7.1 -- IStrategyFormatter<T> interface
- [ ] `/test` -- `src/Conjecture.Tests/Formatting/StrategyFormatterTests.cs`
  - Custom formatter implementing `IStrategyFormatter<int>` formats correctly, `Format(42)` returns expected string
- [ ] `/implement` -- `src/Conjecture.Core/Formatting/IStrategyFormatter.cs`
  - `public interface IStrategyFormatter<T> { string Format(T value); }`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.7.2 -- FormatterRegistry with Holder<T> pattern (ADR-0014)
- [ ] `/test` -- `src/Conjecture.Tests/Formatting/FormatterRegistryTests.cs`
  - `FormatterRegistry.Register<int>(formatter)` then `FormatterRegistry.Get<int>()` returns it, unregistered type returns null, register replaces previous
- [ ] `/implement` -- `src/Conjecture.Core/Formatting/FormatterRegistry.cs`
  - Uses `static class Holder<T> { public static IStrategyFormatter<T>? Instance; }` pattern (NativeAOT-safe, no Type-keyed dictionary)
  - `public static void Register<T>(IStrategyFormatter<T>)` and `public static IStrategyFormatter<T>? Get<T>()`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.7.3 -- Built-in formatters: primitives
- [ ] `/test` -- `src/Conjecture.Tests/Formatting/BuiltInFormatterTests.cs`
  - int → `42`, bool → `true`/`false`, double → `3.14`, float → `1.5f`, string → `"hello"` (with quotes and escaping), byte[] → `new byte[] { 0x01, 0xFF }`
- [ ] `/implement` -- `src/Conjecture.Core/Formatting/BuiltInFormatters.cs`
  - Static class with individual formatter implementations; registers via `[ModuleInitializer]`

#### Cycle 1.7.4 -- Built-in formatters: collections and tuples
- [ ] `/test` -- `src/Conjecture.Tests/Formatting/CollectionFormatterTests.cs`
  - `List<int>` → `[3, -1, 7]`, `HashSet<string>` → set syntax, `Dictionary<int,string>` → `{1: "a", 2: "b"}`, `(int, string)` → `(3, "x")`, empty collections
- [ ] `/implement` -- Extend `src/Conjecture.Core/Formatting/BuiltInFormatters.cs` with collection and tuple formatters
  - Collection formatters use `FormatterRegistry.Get<T>()` for elements; fall back to `ToString()`

#### Cycle 1.7.5 -- Enhanced CounterexampleFormatter (ADR-0022)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/EnhancedCounterexampleFormatterTests.cs`
  - Output includes "Falsifying example found after N examples", includes "Shrunk M times from original", param values use FormatterRegistry when available, falls back to `ToString()`, includes seed line
- [ ] `/implement` -- Modify `src/Conjecture.Core/Internal/CounterexampleFormatter.cs` to use `FormatterRegistry`
  - Extend `TestRunResult` with example count and shrink count metadata

---

### 1.8 Extended Settings (ADR-0016, ADR-0020)

#### Cycle 1.8.1 -- ConjectureSettings extended properties
- [ ] `/test` -- `src/Conjecture.Tests/ConjectureSettingsExtendedTests.cs`
  - `UseDatabase` defaults true, `Deadline` defaults null, `MaxStrategyRejections` defaults 5, `MaxUnsatisfiedRatio` defaults 200, `DatabasePath` defaults `.conjecture/examples/`, negative values throw
- [ ] `/implement` -- Extend `src/Conjecture.Core/ConjectureSettings.cs`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.8.2 -- Settings JSON file loading (ADR-0016)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/SettingsLoaderTests.cs`
  - Loads `.conjecture/settings.json` and parses to `ConjectureSettings`, missing file returns defaults, malformed JSON throws clear error, partial overrides keep defaults
- [ ] `/implement` -- `src/Conjecture.Core/Internal/SettingsLoader.cs`
  - Uses `System.Text.Json` (in BCL)

#### Cycle 1.8.3 -- Assembly-level settings attribute (ADR-0016)
- [ ] `/test` -- `src/Conjecture.Tests/AssemblySettingsAttributeTests.cs`
  - `[assembly: ConjectureSettings(MaxExamples = 500)]` recognized, overrides JSON, test-level overrides assembly-level
- [ ] `/implement` -- `src/Conjecture.Core/ConjectureSettingsAttribute.cs`
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.8.4 -- Settings resolution hierarchy
- [ ] `/test` -- `src/Conjecture.Tests/Internal/SettingsResolverTests.cs`
  - Three-layer resolution: defaults < JSON < assembly attribute < test-level, inner scope wins
- [ ] `/implement` -- `src/Conjecture.Core/Internal/SettingsResolver.cs`

#### Cycle 1.8.5 -- Health check: too many unsatisfied assumptions (ADR-0020)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/HealthCheckTests.cs`
  - TestRunner fails with "too many unsatisfied assumptions" when ratio exceeds `MaxUnsatisfiedRatio`, passes when within budget, respects setting
- [ ] `/implement` -- Modify `src/Conjecture.Core/Internal/TestRunner.cs` to track valid/unsatisfied ratio
  - Add `src/Conjecture.Core/ConjectureException.cs` for non-counterexample failures
  - Update `PublicAPI.Unshipped.txt`

#### Cycle 1.8.6 -- Wire extended settings into PropertyAttribute
- [ ] `/test` -- `src/Conjecture.Tests/PropertyAttributeSettingsTests.cs`
  - `[Property(UseDatabase = false)]` is respected, `[Property(MaxStrategyRejections = 20)]` flows through, Deadline timeout terminates long-running test
- [ ] `/implement` -- Extend `src/Conjecture.Xunit/PropertyAttribute.cs`; modify `PropertyTestCaseRunner` to build full settings via `SettingsResolver`
  - Update `PublicAPI.Unshipped.txt`

---

### 1.9 Example Database (ADR-0012, ADR-0024)

#### Cycle 1.9.1 -- ExampleDatabase schema and initialization
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Database/ExampleDatabaseSchemaTests.cs`
  - Creates `.conjecture/examples/conjecture.db`, schema has version table (version 1), schema has examples table (test_id_hash TEXT, buffer BLOB, created_at TEXT), WAL mode enabled
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Database/ExampleDatabase.cs`
  - Uses `Microsoft.Data.Sqlite`; creates tables on first access; enables WAL mode (ADR-0017)

#### Cycle 1.9.2 -- ExampleDatabase Save and Load
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Database/ExampleDatabaseCrudTests.cs`
  - `Save(testIdHash, buffer)` persists, `Load(testIdHash)` returns saved buffers, empty returns empty list, multiple buffers per test ID returned, duplicate save doesn't create duplicates
- [ ] `/implement` -- Add `Save`, `Load` methods to `ExampleDatabase`

#### Cycle 1.9.3 -- ExampleDatabase Delete
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Database/ExampleDatabaseDeleteTests.cs`
  - `Delete(testIdHash)` removes all buffers, nonexistent key is no-op, Delete then Load returns empty
- [ ] `/implement` -- Add `Delete` method to `ExampleDatabase`

#### Cycle 1.9.4 -- Test ID hashing (ADR-0024)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/Database/TestIdHasherTests.cs`
  - Same fully-qualified name produces same hash, different names diverge, stable across runs, includes parameter types
- [ ] `/implement` -- `src/Conjecture.Core/Internal/Database/TestIdHasher.cs`
  - SHA256 of `namespace.class.method(paramType1,paramType2)`

#### Cycle 1.9.5 -- Wire database into TestRunner (ADR-0024)
- [ ] `/test` -- `src/Conjecture.Tests/Internal/TestRunnerDatabaseTests.cs`
  - `UseDatabase=true` + failing test saves buffer, next run replays stored buffer first, passing replay removes buffer, `UseDatabase=false` skips DB, explicit `Seed` skips DB
- [ ] `/implement` -- Modify `src/Conjecture.Core/Internal/TestRunner.cs`
  - `TestRunner.Run` extended to accept optional `ExampleDatabase` + test ID hash
  - Replay stored buffers before generating new; save on failure; delete on pass

#### Cycle 1.9.6 -- Wire database into PropertyTestCaseRunner
- [ ] `/test` -- `src/Conjecture.Tests/PropertyAttributeDatabaseTests.cs`
  - `[Property]` that fails stores counterexample in DB, re-run replays stored failure, after fix stored buffer cleaned up
- [ ] `/implement` -- Modify `src/Conjecture.Xunit/Internal/PropertyTestCaseRunner.cs`
  - Create `ExampleDatabase` + `TestIdHasher`, pass to `TestRunner`

---

### 1.10 Extended ParameterStrategyResolver & End-to-End Tests

#### Cycle 1.10.1 -- ParameterStrategyResolver extended types
- [ ] `/test` -- `src/Conjecture.Tests/ParameterStrategyResolverExtendedTests.cs`
  - Resolves `string`, `float`, `double`, `List<int>`, enum, nullable `int?` params; unsupported type gives clear error
- [ ] `/implement` -- Extend `src/Conjecture.Xunit/Internal/ParameterStrategyResolver.cs`
  - `string` → `Gen.Strings()`, `float` → `Gen.Floats()`, `double` → `Gen.Doubles()`, `List<int>` → `Gen.Lists(Gen.Integers<int>())`, enums → `Gen.Enums<T>()`, nullable → `Gen.Nullable(...)`

#### Cycle 1.10.2 -- End-to-end: string property tests
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/StringPropertyE2ETests.cs`
  - `[Property]` with string param runs, failing test shrinks to minimal string, formatted output shows string in quotes

#### Cycle 1.10.3 -- End-to-end: floating point property tests
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/FloatingPointPropertyE2ETests.cs`
  - `[Property]` with double param runs, `[Property]` with float param runs, special values (NaN) can be generated

#### Cycle 1.10.4 -- End-to-end: collection property tests
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/CollectionPropertyE2ETests.cs`
  - `[Property]` with `List<int>` param runs and shrinks, list shrinks toward empty/minimal, formatted output shows list as `[1, 2, 3]`

#### Cycle 1.10.5 -- End-to-end: formatter integration
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/FormatterE2ETests.cs`
  - Failure output shows "Falsifying example found after N examples", includes shrink count, values use registered formatters, custom formatter works

#### Cycle 1.10.6 -- End-to-end: database regression
- [ ] `/test` -- `src/Conjecture.Tests/EndToEnd/DatabaseRegressionE2ETests.cs`
  - Full round-trip: fail → save → replay → fix → clean, DB file exists after failure, seed printed alongside DB storage

---

## Post-implementation

- [ ] `/benchmark` -- Perf: FloatingPointStrategy, StringStrategy, ListStrategy generation, ExampleDatabase Save/Load, FormatterRegistry lookup; establish baselines (ADR-0025)
- [ ] Full verification: `dotnet test src/`

## Key Constraints

- **`PublicAPI.Unshipped.txt`** updated every cycle that adds public API (ADR-0002, ADR-0003)
- **NativeAOT safe** -- `FormatterRegistry` uses `Holder<T>` generic statics, no Type-keyed dictionaries (ADR-0014)
- **ArrayPool<byte> + Span<T>** for buffers (ADR-0009)
- **Nullable enabled, warnings as errors** -- null annotations on all public API
- **Internal by default** -- only `Gen`, `Strategy<T>`, `StrategyExtensions`, `Strategies`, `IGeneratorContext`, `Assume`, `ConjectureSettings`, `PropertyAttribute`, `IStrategyFormatter<T>`, `FormatterRegistry`, `ConjectureException` are public
- **File-scoped namespaces** throughout
- **.NET 10 minimum** -- C# 14 features and generic math (`INumber<T>`, `IFloatingPoint<T>`) are available (ADR-0006)
- **No speculative optimization** -- profile before optimizing; BenchmarkDotNet baselines required post-Phase 1 (ADR-0025)
- **SQLite WAL mode** for example database concurrency (ADR-0017)
- **`IBinaryFloatingPointIeee754<T>`** for float/double strategies (ADR-0011 extension)
- **No runtime reflection in public API paths** (ADR-0014) -- `ParameterStrategyResolver` is internal/xUnit-only
- Use `/decision` if design questions arise not covered by existing ADRs

## Verification

After each sub-phase:
```bash
dotnet build src/
dotnet test src/
```

After 1.5 (collection strategies):
```bash
dotnet test src/ --filter "FullyQualifiedName~StrategyTests"
```

After 1.7 (formatters):
```bash
dotnet test src/ --filter "FullyQualifiedName~Formatter"
```

After 1.9 (database):
```bash
dotnet test src/ --filter "FullyQualifiedName~Database"
```

End-to-end after 1.10:
```bash
dotnet test src/ --filter "FullyQualifiedName~EndToEnd"
```

Final:
```bash
dotnet test src/
dotnet build src/ -c Release
```
