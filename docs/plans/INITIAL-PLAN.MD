# Conjecture — Implementation Plan

## A full port of Python's Hypothesis property-based testing library to C# / .NET

---

## 1. Executive Summary

This document outlines the implementation plan for **Conjecture**, a ground-up port of Python's [Hypothesis](https://hypothesis.readthedocs.io/) property-based testing library to C# and .NET. The goal is to bring Hypothesis's core innovation — the **Conjecture engine** with byte-stream-backed test case generation and example-aware shrinking — to the .NET ecosystem as an open-source project.

### Why this matters

The .NET ecosystem has property-based testing libraries (FsCheck, CsCheck), but none replicate Hypothesis's key architectural insight: representing all generated test data as a sequence of bytes (a "choice sequence"), which enables **type-independent shrinking**, **targeted testing**, and a **database of failing examples**. This approach produces dramatically better minimal counterexamples and more reliable reproduction than traditional generator-with-shrinker designs.

### Key design decisions

- **Target:** .NET 10 (LTS, supported until November 2028) — takes full advantage of C# 14 and .NET 10 JIT/runtime improvements (see Section 3.7)
- **Test framework integration:** xUnit first, NUnit and MSTest adapters second
- **Language:** C# (not F# — broader .NET audience, easier onboarding for contributors)
- **License:** MPL-2.0 (matching Hypothesis itself)
- **Package:** NuGet distribution as `Conjecture`

---

## 2. Architecture Overview

Hypothesis has a layered architecture. The port should preserve these layers:

```
┌──────────────────────────────────────────────┐
│  User-Facing API                             │
│  [Fact], [Property], Fluent strategy builders│
├──────────────────────────────────────────────┤
│  Strategy Layer                              │
│  Strategies<T>, .Map(), .Filter(), .FlatMap()│
├──────────────────────────────────────────────┤
│  Conjecture Engine                           │
│  ConjectureData, choice sequence, shrinking  │
├──────────────────────────────────────────────┤
│  Test Runner / Integration                   │
│  xUnit integration, settings, reporting      │
├──────────────────────────────────────────────┤
│  Database Layer                              │
│  Persistent example storage & replay         │
└──────────────────────────────────────────────┘
```

### 2.1 Core Concepts to Preserve

| Python Hypothesis concept | C# equivalent approach |
|---|---|
| `ConjectureData` | `ConjectureData` class — pooled `byte[]` buffer, `Span<byte>` reads, `IDisposable` |
| `data.draw(strategy)` | `strategy.Draw(data)` (internal) / `strategy.Next()` (public) |
| `@st.composite` | `Strategies.Compose<T>(gen => { ... })` + LINQ query syntax |
| `@given(...)` decorator | `[Property]` attribute + xUnit integration |
| `st.integers()`, `st.text()`, etc. | `Strategies.Numbers<T>()` via `INumber<T>`, `Strategies.Strings()`, etc. |
| `.map()`, `.filter()`, `.flatmap()` | `.Select()`, `.Where()`, `.SelectMany()` (LINQ naming) |
| `assume()` | `Assume.That(condition)` |
| `st.builds(MyClass, ...)` | `[Arbitrary]` source generator + `Builds<T>()` fluent fallback |
| Settings / profiles | `ConjectureSettings` record with builder pattern |
| Example database | `IExampleDatabase` with SQLite/file-based default |
| `@example(...)` | `[Example(...)]` attribute for explicit cases |
| Stateful testing (`RuleBasedStateMachine`) | Phase 3 — `StateMachine<TState>` base class |

---

## 3. API Design Principles — .NET Native

A port that feels native is one where users never think about the Python origins. Every public surface should be idiomatic C# that .NET developers discover through IntelliSense alone.

### 3.1 XML Documentation (`///`) as a First-Class Requirement

Every public and protected member must have complete XML documentation. Enforce at compile time:

```xml
<!-- In Directory.Build.props (applies to all projects) -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <NoWarn>$(NoWarn);CS1573</NoWarn> <!-- Allow undocumented params on overrides -->
</PropertyGroup>
```

Documentation standards:

```csharp
/// <summary>
/// Generates random integers uniformly distributed within the specified range.
/// Values shrink toward <paramref name="min"/>.
/// </summary>
/// <param name="min">
/// The inclusive lower bound. Defaults to <see cref="int.MinValue"/>.
/// </param>
/// <param name="max">
/// The inclusive upper bound. Defaults to <see cref="int.MaxValue"/>.
/// </param>
/// <returns>
/// A <see cref="Strategy{T}"/> that produces <see cref="int"/> values
/// in [<paramref name="min"/>, <paramref name="max"/>].
/// </returns>
/// <exception cref="ArgumentOutOfRangeException">
/// Thrown when <paramref name="min"/> is greater than <paramref name="max"/>.
/// </exception>
/// <example>
/// <code>
/// // Generate ages between 0 and 150
/// var ages = Strategies.Integers(min: 0, max: 150);
///
/// // Use in a property test
/// [Property]
/// public void AgeIsNonNegative(
///     [From(nameof(ages))] int age) => Assert.True(age >= 0);
/// </code>
/// </example>
/// <remarks>
/// The shrinking behavior always moves toward <paramref name="min"/>, producing
/// the simplest possible counterexample. For unsigned ranges, consider
/// <see cref="Integers(uint, uint)"/> to avoid unnecessary sign handling.
/// </remarks>
public static Strategy<int> Integers(int min = int.MinValue, int max = int.MaxValue)
```

Required tags by member type:

| Member type | Required tags |
|---|---|
| Class / struct / interface | `<summary>`, `<remarks>` if non-obvious, `<typeparam>` for generics |
| Method | `<summary>`, `<param>` for each parameter, `<returns>`, `<exception>` if throws |
| Property | `<summary>`, `<value>` for non-obvious semantics |
| Constructor | `<summary>`, `<param>` for each parameter |
| Strategy factory method | All of the above + `<example>` block showing typical usage |

### 3.2 Naming & Idiom Conventions

| Python Hypothesis | Conjecture | Rationale |
|---|---|---|
| `st.integers()` | `Strategies.Integers()` / `Strategies.Numbers<int>()` | Convenience alias + unified `INumber<T>` generic |
| `st.text()` | `Strategies.Strings()` | .NET uses `string`, not `text` |
| `data.draw(strategy)` | `strategy.Draw(data)` (internal) | Preserves Python mapping for contributors porting code |
| `strategy.example()` | `strategy.Next()` (public) | .NET-idiomatic; `Draw` feels foreign in C# public APIs |
| `@st.composite` | `Strategies.Compose<T>(gen => { ... })` | Imperative strategy building with `IGeneratorContext` |
| `.map(f)` | `.Select(f)` | LINQ convention |
| `.filter(f)` | `.Where(f)` | LINQ convention |
| `.flatmap(f)` | `.SelectMany(f)` | LINQ convention |
| `@given(st.integers())` | `[Property]` + type inference | Attribute-driven |
| `@given(st.integers(1, 100))` | `[From<PositiveInts>]` | Generic attribute (C# 11), compile-time safe |
| `assume(x > 0)` | `Assume.That(x > 0)` | Fluent assertion style |
| `max_examples=100` | `MaxExamples = 100` | PascalCase properties |
| `settings(...)` | `ConjectureSettings` record | Immutable record with `with` syntax |
| `HealthCheck.too_slow` | `HealthCheck.TooSlow` | PascalCase enum values |
| `note("...")` | `TestContext.Note("...")` | xUnit-aligned naming |

Additional conventions:
- **No underscore prefix on private fields** — use `camelCase` without leading `_` (e.g., `private readonly int maxSize;` not `private readonly int _maxSize;`). Enforce via `.editorconfig`:
  ```ini
  [*.cs]
  dotnet_naming_rule.private_fields_no_prefix.severity = error
  dotnet_naming_rule.private_fields_no_prefix.symbols = private_fields
  dotnet_naming_rule.private_fields_no_prefix.style = camel_case
  dotnet_naming_symbols.private_fields.applicable_kinds = field
  dotnet_naming_symbols.private_fields.applicable_accessibilities = private, private_protected
  dotnet_naming_style.camel_case.capitalization = camel_case
  ```
- Use `readonly record struct` for small value types (e.g., `Block`, `DrawResult`)
- Use `sealed` on all classes not designed for inheritance
- Nullable reference types enabled everywhere (`<Nullable>enable</Nullable>`)
- `ConfigureAwait(false)` on all internal async calls
- `EditorBrowsable(EditorBrowsableState.Never)` on internal-but-public members (needed for cross-assembly access)

### 3.3 Memory Model — `Span<T>`, `Memory<T>`, and Buffer Management

The Conjecture engine's byte stream is the hottest path in the library. Every generated test example reads from and writes to this buffer, and the shrinker may replay thousands of mutations per test case. This makes it an ideal candidate for high-performance .NET memory primitives.

#### Buffer architecture

```csharp
/// <summary>
/// Manages the underlying byte stream that backs all random data generation.
/// Each test example is fully determined by its buffer contents.
/// </summary>
public sealed class ConjectureData : IDisposable
{
    // The buffer is rented from the shared pool to avoid GC pressure
    // during rapid generate-shrink cycles.
    private byte[] rentedBuffer;
    private int length;        // logical length (may be < rentedBuffer.Length)
    private int index;         // current read position
    private readonly List<Block> blocks;

    /// <summary>
    /// Returns the portion of the buffer that has been consumed so far,
    /// without allocating a new array.
    /// </summary>
    public ReadOnlySpan<byte> ConsumedBytes => rentedBuffer.AsSpan(0, index);

    /// <summary>
    /// Returns the remaining unread portion of the buffer.
    /// </summary>
    public ReadOnlySpan<byte> RemainingBytes => rentedBuffer.AsSpan(index, length - index);

    /// <summary>
    /// Reads <paramref name="count"/> bytes from the buffer without copying.
    /// </summary>
    /// <returns>
    /// A span over the requested bytes. Valid only until the next draw operation.
    /// </returns>
    internal ReadOnlySpan<byte> DrawBytes(int count)
    {
        if (index + count > length)
        {
            Status = Status.Overrun;
            return ReadOnlySpan<byte>.Empty;
        }

        var span = rentedBuffer.AsSpan(index, count);
        blocks.Add(new Block(index, count, currentLabel));
        index += count;
        return span;
    }

    /// <summary>
    /// Reads an integer from the buffer using variable-width encoding.
    /// </summary>
    public ulong DrawInteger(ulong min, ulong max)
    {
        int bytesNeeded = BytesForRange(max - min);
        ReadOnlySpan<byte> raw = DrawBytes(bytesNeeded);
        if (Status == Status.Overrun) return 0;
        return min + ReadBigEndian(raw) % (max - min + 1);
    }

    public void Dispose()
    {
        if (rentedBuffer != null)
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            rentedBuffer = null!;
        }
    }
}
```

#### Where Span vs Memory vs byte[]

| Context | Type | Reason |
|---|---|---|
| Drawing bytes within a single method | `ReadOnlySpan<byte>` | Stack-only, zero-alloc, fastest reads |
| Storing buffer slices across async boundaries | `ReadOnlyMemory<byte>` | Heap-safe, can cross `await` points |
| Database persistence / serialization | `byte[]` | Must materialize for I/O |
| Shrinker buffer mutations | `Span<byte>` (writable) | In-place mutation of candidate buffers |
| Cross-method block references | `Block` struct with (offset, length) | Reference into the buffer without copying |

#### ArrayPool usage

```csharp
/// <summary>
/// Creates a new ConjectureData backed by a pooled buffer.
/// Call <see cref="Dispose"/> to return the buffer to the pool.
/// </summary>
public static ConjectureData Create(int maxSize = 8192)
{
    byte[] buffer = ArrayPool<byte>.Shared.Rent(maxSize);
    return new ConjectureData(buffer, maxSize);
}
```

Key principles:
- **Rent from `ArrayPool<byte>.Shared`** for all engine-internal buffers. During shrinking, the engine may create and discard thousands of candidate buffers — pooling eliminates the GC pressure.
- **Use `Span<byte>` for all synchronous read/write paths** inside the engine. Draw operations, shrinker mutations, and block comparisons are all synchronous and Span-eligible.
- **Materialize to `byte[]` only at persistence boundaries** — when saving to the example database or serializing for diagnostics.
- **Use `stackalloc` for small temporary buffers** (e.g., encoding a single integer):

```csharp
internal static ulong ReadBigEndian(ReadOnlySpan<byte> span)
{
    ulong result = 0;
    for (int i = 0; i < span.Length; i++)
        result = (result << 8) | span[i];
    return result;
}
```

#### Shrinker buffer management

The shrinker is the heaviest consumer of buffer operations. The naive approach clones the entire buffer for each shrink attempt:
1. Clone current best buffer
2. Apply a mutation (zero a block, delete bytes, swap blocks)
3. Re-run the test with the mutated buffer
4. Keep the mutation if the test still fails, otherwise discard

This works, but for a buffer of 4–8 KB and thousands of shrink attempts, the cumulative `memcpy` cost is measurable. **Immutable collections won't help** — `ImmutableArray<byte>` is a flat array wrapper with no structural sharing, so it still copies the whole buffer on every mutation.

Instead, use an **in-place mutation with rollback** strategy that avoids cloning entirely for most shrink passes:

```csharp
/// <summary>
/// Applies a shrink mutation directly to the current best buffer.
/// If the mutation doesn't improve the result, rolls back the
/// affected bytes without copying the entire buffer.
/// </summary>
/// <remarks>
/// Most shrink passes modify a small, contiguous region of the buffer
/// (a single block). Saving and restoring just that region is far cheaper
/// than cloning the entire buffer. For a 4 KB buffer where a typical block
/// is 4–8 bytes, this reduces copy volume by ~500x per attempt.
/// </remarks>
internal ref struct ShrinkMutation
{
    private readonly Span<byte> buffer;
    private readonly int offset;
    private readonly int length;
    private readonly Span<byte> saved;  // stack-allocated backup of the affected region

    /// <summary>
    /// Saves the region about to be mutated. Call before applying changes.
    /// </summary>
    public ShrinkMutation(Span<byte> buffer, int offset, int length, Span<byte> scratchpad)
    {
        this.buffer = buffer;
        this.offset = offset;
        this.length = length;
        saved = scratchpad[..length];
        buffer.Slice(offset, length).CopyTo(saved);  // save only the affected region
    }

    /// <summary>
    /// Restores the original bytes if the mutation didn't improve the result.
    /// </summary>
    public void Rollback()
    {
        saved.CopyTo(buffer.Slice(offset, length));
    }
}
```

**Usage in a shrink pass:**

```csharp
internal bool TryZeroBlock(Span<byte> buffer, Block block)
{
    // Stack-allocate the backup for small blocks (common case)
    Span<byte> scratchpad = block.Length <= 128
        ? stackalloc byte[block.Length]
        : ArrayPool<byte>.Shared.Rent(block.Length);

    var mutation = new ShrinkMutation(buffer, block.Offset, block.Length, scratchpad);

    // Mutate in place — zero the block
    buffer.Slice(block.Offset, block.Length).Clear();

    // Re-run the test with the mutated buffer
    bool stillInteresting = RerunTest(buffer);

    if (!stillInteresting)
        mutation.Rollback();  // restore just the affected bytes

    if (block.Length > 128)
        ArrayPool<byte>.Shared.Return(scratchpad.ToArray());

    return stillInteresting;
}
```

**When full cloning is still needed:**

Some shrink passes change the buffer's *length* (e.g., deleting blocks removes bytes, which shifts all subsequent data). These structural mutations can't use in-place rollback because the original layout is lost. For these passes, fall back to the full clone approach:

```csharp
internal sealed class ShrinkAttempt : IDisposable
{
    private byte[] candidate;

    /// <summary>
    /// Full clone — used only for structural mutations that change buffer length
    /// (block deletion, interval removal). Most passes use in-place mutation instead.
    /// </summary>
    public ShrinkAttempt(ReadOnlySpan<byte> currentBest, int maxSize)
    {
        candidate = ArrayPool<byte>.Shared.Rent(maxSize);
        currentBest.CopyTo(candidate.AsSpan());
    }

    public Span<byte> Buffer => candidate.AsSpan();
    public void Dispose() => ArrayPool<byte>.Shared.Return(candidate);
}
```

**Performance impact by shrink pass type:**

| Pass type | Buffer change | Strategy | Copy volume per attempt |
|---|---|---|---|
| Zero a block | Same length, localized | In-place + rollback | 4–8 bytes (block size only) |
| Lexicographic minimize | Same length, localized | In-place + rollback | 4–8 bytes |
| Reduce integer | Same length, localized | In-place + rollback | 1–8 bytes |
| Swap blocks | Same length, two regions | In-place + rollback (both regions) | 8–16 bytes |
| Redistribute | Same length, two regions | In-place + rollback | 8–16 bytes |
| Delete blocks | **Shorter** buffer | Full clone | Full buffer (4–8 KB) |
| Interval deletion | **Shorter** buffer | Full clone | Full buffer (4–8 KB) |

In practice, ~70–80% of shrink attempts are same-length mutations that benefit from in-place rollback. The `ref struct` ShrinkMutation lives entirely on the stack (no heap allocation), and `stackalloc` handles the backup for blocks ≤128 bytes (which covers nearly all cases).

**Note on `ref struct`:** `ShrinkMutation` is a `ref struct` because it holds `Span<byte>` fields. This means it can't be stored in fields, boxed, or used across async boundaries — which is exactly right, since it's scoped to a single synchronous shrink attempt.
```

### 3.4 Thread Safety & Parallelism

xUnit runs test classes in parallel by default. NUnit and MSTest have similar capabilities. The library must handle this safely without requiring users to think about it.

#### Threading model

```
┌────────────────────────────────────────────────┐
│  xUnit Test Runner (parallel by default)       │
│                                                │
│  Thread 1          Thread 2         Thread N   │
│  ┌──────────┐     ┌──────────┐    ┌──────────┐│
│  │Property A│     │Property B│    │Property C││
│  │          │     │          │    │          ││
│  │ Engine   │     │ Engine   │    │ Engine   ││
│  │ instance │     │ instance │    │ instance ││
│  │ (own     │     │ (own     │    │ (own     ││
│  │  buffer) │     │  buffer) │    │  buffer) ││
│  └────┬─────┘     └────┬─────┘    └────┬─────┘│
│       │                │               │       │
│       └───────┬────────┴───────────────┘       │
│               ▼                                │
│    ┌─────────────────────┐                     │
│    │  Shared Resources   │                     │
│    │  - ExampleDatabase  │ (thread-safe)       │
│    │  - Settings         │ (immutable)         │
│    │  - StrategyRegistry │ (read-only)         │
│    └─────────────────────┘                     │
└────────────────────────────────────────────────┘
```

#### Design rules

**1. Per-test-case isolation (no shared mutable state in the hot path)**

Each property test execution gets its own `ConjectureEngine` and `ConjectureData` instances. No locking required in the generation/shrink loop because nothing is shared.

```csharp
/// <summary>
/// Entry point for running a single property test. Creates an isolated
/// engine instance — safe to call from multiple threads concurrently.
/// </summary>
public static PropertyResult Run<T>(
    Strategy<T> strategy,
    Action<T> test,
    ConjectureSettings? settings = null)
{
    // Each call gets its own engine — no shared mutable state
    using var engine = new ConjectureEngine(settings ?? ConjectureSettings.Default);
    return engine.Execute(strategy, test);
}
```

**2. Thread-safe shared resources**

Only three things are shared across parallel test runs:

| Resource | Strategy | Implementation |
|---|---|---|
| Example database | Concurrent access to filesystem/SQLite | File-level locking or `ConcurrentDictionary` for in-memory; SQLite handles its own locking |
| `ConjectureSettings.Default` | Immutable after initialization | `record` type — naturally thread-safe. Set once at startup. |
| Strategy registry (type → strategy mappings) | Read-only after initialization | Generic static `Holder<T>` pattern — zero boxing, zero lookup cost (see Section 3.5) |

```csharp
/// <summary>
/// Thread-safe example database using file-system storage with
/// advisory file locking to prevent corruption under parallel test runs.
/// </summary>
public sealed class DirectoryBasedDatabase : IExampleDatabase
{
    private readonly string basePath;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

    public async Task SaveAsync(string key, byte[] buffer,
        CancellationToken cancellationToken = default)
    {
        var semaphore = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var path = GetPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, buffer, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

**3. Random number generation**

`System.Random` is not thread-safe. Use `Random.Shared` (.NET 6+) for seed generation and `RandomNumberGenerator` for cryptographic seeding, but critically, each `ConjectureEngine` instance holds its own `Random` — no sharing.

```csharp
public sealed class ConjectureEngine : IDisposable
{
    private readonly Random rng;  // private to this engine instance

    /// <summary>
    /// Creates an engine with an isolated random source.
    /// Thread-safe by isolation — each test gets its own engine.
    /// </summary>
    public ConjectureEngine(ConjectureSettings settings)
    {
        rng = settings.Seed.HasValue
            ? new Random((int)settings.Seed.Value)
            : new Random(Random.Shared.Next());  // Random.Shared is thread-safe
    }
}
```

**4. CancellationToken support throughout**

All async public APIs accept `CancellationToken` for cooperative cancellation — essential for test timeouts and CI pipeline integration.

```csharp
public interface IExampleDatabase
{
    /// <summary>
    /// Persists a failing example's buffer for future replay.
    /// </summary>
    Task SaveAsync(string key, byte[] buffer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a previously saved failing example, or <c>null</c> if none exists.
    /// </summary>
    Task<byte[]?> LoadAsync(string key,
        CancellationToken cancellationToken = default);
}
```

**5. What NOT to parallelize**

The shrink loop for a *single* test case is inherently sequential — each shrink pass depends on the result of the previous one. Don't try to parallelize this. The parallelism comes from running *different* property tests concurrently, which is handled naturally by the test runner.

### 3.5 Boxing Avoidance

The Conjecture engine's hot path — draw, evaluate, shrink, repeat — can execute millions of iterations during a single property test. Boxing a value type even once per draw adds GC pressure that compounds across the shrink loop. The design must eliminate boxing from all hot paths.

#### Boxing risk inventory

| Location | Risk | Cause | Mitigation |
|---|---|---|---|
| `DrawInteger` return value | **None** | Returns `ulong` → assigned to `ulong` | Already unboxed |
| Strategy draw → test function | **Low** | `Strategy<T>.Draw()` returns `T` directly | Generic dispatch avoids boxing |
| Block metadata | **HIGH** | Storing drawn value as `object` for diagnostics | Store only byte range (offset + length), reconstruct on demand |
| Strategy registry | **HIGH** | `Dictionary<Type, object>` stores `Strategy<int>` as `object` | Use generic accessor pattern (see below) |
| Shrink result tracking | **MEDIUM** | Storing "best result" as `object` for type-erased engine | Keep result as `byte[]` buffer only — reconstruct typed value at reporting time |
| OneOf / composite strategies | **MEDIUM** | Heterogeneous strategy arrays may use `Strategy<object>` covariance | Use discriminated union pattern or `IStrategy` with generic `Execute<TResult>` visitor |
| Event/diagnostics callbacks | **LOW** | `Action<object>` style event handlers | Use generic `Action<T>` or `EventHandler<DrawEventArgs<T>>` |

#### Pattern 1: Type-safe strategy registry (no boxing)

```csharp
/// <summary>
/// Provides type-safe storage and retrieval of default strategies
/// without boxing value-type strategies.
/// </summary>
public static class StrategyRegistry
{
    /// <summary>
    /// Thread-safe, lock-free storage using a static generic class —
    /// one field per closed generic type, no dictionary lookup, no boxing.
    /// </summary>
    private static class Holder<T>
    {
        public static volatile Strategy<T>? Default;
    }

    /// <summary>
    /// Registers the default strategy for type <typeparamref name="T"/>.
    /// Call during initialization only.
    /// </summary>
    public static void Register<T>(Strategy<T> strategy)
        => Holder<T>.Default = strategy;

    /// <summary>
    /// Retrieves the default strategy for <typeparamref name="T"/>,
    /// or <c>null</c> if none is registered. Zero allocations, no boxing.
    /// </summary>
    public static Strategy<T>? Get<T>()
        => Holder<T>.Default;
}
```

This pattern uses the CLR's own generic type specialization — `Holder<int>` and `Holder<string>` are completely separate types with separate static fields. No dictionary, no boxing, no lookup cost, and thread-safe via `volatile`.

#### Pattern 2: Block stores byte ranges, not values

```csharp
/// <summary>
/// Records the location of a single draw operation within the byte buffer.
/// Stores only the byte range — the actual value is reconstructed from the
/// buffer when needed, avoiding boxing of the drawn value.
/// </summary>
/// <remarks>
/// Blocks are the fundamental unit of shrinking. The shrinker manipulates
/// blocks (zero, delete, swap, minimize) rather than typed values,
/// which is what enables type-independent shrinking.
/// </remarks>
public readonly record struct Block
{
    /// <summary>Byte offset within the buffer where this draw begins.</summary>
    public required int Offset { get; init; }

    /// <summary>Number of bytes consumed by this draw.</summary>
    public required int Length { get; init; }

    /// <summary>
    /// Semantic label for diagnostics (e.g., "integer", "listlength").
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Reconstructs the drawn value from the buffer without boxing.
    /// Only called during failure reporting, never in the hot path.
    /// </summary>
    public ulong ReconstructInteger(ReadOnlySpan<byte> buffer)
    {
        var slice = buffer.Slice(Offset, Length);
        ulong result = 0;
        for (int i = 0; i < slice.Length; i++)
            result = (result << 8) | slice[i];
        return result;
    }
}
```

Key: `Block` is a `readonly record struct` (no heap allocation) and stores no `object` fields. The drawn value only materializes as a typed value at the strategy layer (which is generic and therefore unboxed) or at reporting time.

#### Pattern 3: Generic engine execution avoids type erasure

```csharp
/// <summary>
/// Executes a property test with the given strategy. The entire pipeline
/// from draw through test execution stays in generic context — no boxing.
/// </summary>
public PropertyResult Execute<T>(Strategy<T> strategy, Action<T> test)
{
    while (HasBudget)
    {
        using var data = ConjectureData.Create(bufferSize);
        FillBuffer(data, rng);

        // strategy.Draw returns T directly — no boxing
        T value = strategy.Draw(data);

        if (data.Status == Status.Overrun || data.Status == Status.Invalid)
            continue;

        try
        {
            // test receives T directly — no boxing
            test(value);
            RecordValid(data);
        }
        catch (Exception ex)
        {
            // Store the *buffer*, not the *value* — buffer is byte[],
            // value reconstruction happens only at report time
            RecordInteresting(data, ex);
        }
    }

    return BuildResult();
}
```

The critical insight: the engine never needs to store `T` — it stores `byte[]` buffers. The typed value is ephemeral: it's created by `strategy.Draw()`, passed to `test()`, and then discarded. No field of type `object` or `T` is needed on the engine itself.

#### Pattern 4: Avoid LINQ on value-type collections in hot paths

```csharp
// ❌ Boxing risk — LINQ on List<Block> may box the struct enumerator
var interesting = blocks.Where(b => b.Length > 0).Select(b => b.Offset);

// ✅ No boxing — manual iteration, stack-only
for (int i = 0; i < blocks.Count; i++)
{
    ref readonly var block = ref CollectionsMarshal.AsSpan(blocks)[i];
    if (block.Length > 0)
        ProcessBlock(block);
}
```

For the shrinker's inner loops (which iterate over blocks thousands of times), prefer `for` loops with `CollectionsMarshal.AsSpan` to avoid the `List<T>.Enumerator` struct boxing through `IEnumerator<T>` and to get `ref readonly` access without copying the struct.

#### Validation strategy

Add a BenchmarkDotNet benchmark that runs a simple property test and tracks allocations:

```csharp
[MemoryDiagnoser]
public class BoxingBenchmarks
{
    [Benchmark]
    public void IntegerStrategy_NoBoxing()
    {
        var result = PropertyRunner.Run(
            Strategies.Integers(0, 1000),
            x => { },  // no-op test — measure engine overhead only
            new ConjectureSettings { MaxExamples = 1000 });
    }
}
```

Target: **zero allocations per draw operation** in the steady-state loop. `Gen 0` collections in the benchmark should come only from buffer management (pooled) and strategy composition (one-time setup), never from per-draw boxing.

### 3.6 Generic Attributes & `INumber<T>` Numeric Strategies

#### Generic attributes for strategy binding (C# 11+)

The original plan used string-based strategy attributes (`[Strategy("Integers(1, 1000)")]`), which require runtime parsing, give no IntelliSense, and break silently on typos. C# 11 generic attributes eliminate all of these problems.

**Design:**

```csharp
/// <summary>
/// Specifies a strategy type to use for generating this parameter's values.
/// The strategy type must implement <see cref="Strategy{T}"/> and have a
/// parameterless constructor.
/// </summary>
/// <typeparam name="TStrategy">
/// A <see cref="Strategy{T}"/> subclass with a parameterless constructor.
/// The strategy's output type must be assignable to the parameter type.
/// </typeparam>
/// <example>
/// <code>
/// [Property]
/// public void PositiveMultiplicationCommutes(
///     [From&lt;PositiveIntegers&gt;] int a,
///     [From&lt;PositiveIntegers&gt;] int b)
/// {
///     Assert.Equal(a * b, b * a);
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromAttribute<TStrategy> : Attribute
    where TStrategy : IStrategyMarker, new()
{
}

/// <summary>
/// Marker interface that all strategy types implement. Enables the generic
/// constraint on <see cref="FromAttribute{TStrategy}"/> without requiring
/// the attribute itself to carry the output type parameter.
/// </summary>
public interface IStrategyMarker { }

/// <summary>
/// Base class for all strategies. Implements <see cref="IStrategyMarker"/>
/// so strategies can be used with <see cref="FromAttribute{TStrategy}"/>.
/// </summary>
public abstract class Strategy<T> : IStrategyMarker
{
    internal abstract T Draw(ConjectureData data);
    
    // LINQ combinators...
}
```

**Configurable strategies via nested generic types:**

For strategies that need parameters (ranges, lengths, etc.), provide pre-built named types and a base class for custom configurations:

```csharp
/// <summary>
/// Generates integers in [1, int.MaxValue]. Use as
/// <c>[From&lt;PositiveInts&gt;]</c> on property test parameters.
/// </summary>
public sealed class PositiveInts : NumberStrategy<int>
{
    public PositiveInts() : base(min: 1) { }
}

/// <summary>
/// Generates integers in [0, 100]. Useful for percentage values,
/// small collection sizes, and similar bounded domains.
/// </summary>
public sealed class SmallInts : NumberStrategy<int>
{
    public SmallInts() : base(min: 0, max: 100) { }
}

/// <summary>
/// Users define custom strategies by subclassing. One-liner, full type safety:
/// </summary>
/// <example>
/// <code>
/// // Define once
/// public sealed class ValidAge : NumberStrategy&lt;int&gt;
/// {
///     public ValidAge() : base(min: 0, max: 150) { }
/// }
///
/// // Use everywhere — compiler checks the type matches
/// [Property]
/// public void AgeIsValid([From&lt;ValidAge&gt;] int age)
///     => Assert.InRange(age, 0, 150);
/// </code>
/// </example>
public class NumberStrategy<T> : Strategy<T> where T : INumber<T>
{
    // ...
}
```

**Comparison — string vs generic attribute:**

| Aspect | `[Strategy("Integers(1, 1000)")]` | `[From<PositiveInts>]` |
|---|---|---|
| Compile-time type safety | None — fails at runtime | Full — compiler enforces type match |
| IntelliSense | None | Full — autocomplete, go-to-definition |
| Refactoring | Strings don't rename | Rename symbol works across solution |
| Runtime overhead | String parsing + reflection | Direct instantiation via `new()` |
| Discoverability | Must read docs | Browse available strategies in IDE |
| Custom ranges | Limited DSL in string | Subclass with constructor parameters |

**Fallback: `[FromFactory]` for inline lambda strategies**

For one-off cases where creating a named class feels heavy, provide a factory method attribute:

```csharp
/// <summary>
/// References a static method that returns a <see cref="Strategy{T}"/>
/// for this parameter. Use for one-off strategies that don't warrant
/// a dedicated type.
/// </summary>
/// <example>
/// <code>
/// [Property]
/// public void Test([FromFactory(nameof(EvenNumbers))] int n)
///     => Assert.Equal(0, n % 2);
///
/// static Strategy&lt;int&gt; EvenNumbers()
///     => Strategies.Numbers&lt;int&gt;(min: 0, max: 1000).Where(n => n % 2 == 0);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromFactoryAttribute : Attribute
{
    public FromFactoryAttribute(string methodName) { }
}
```

#### Unified numeric strategies with `INumber<T>`

Instead of separate `Integers()`, `Longs()`, `Doubles()`, `Floats()`, `Decimals()` methods, use a single generic entry point constrained to `INumber<T>`. This covers all built-in numeric types and any user-defined types implementing the interface.

```csharp
/// <summary>
/// Generates numeric values of type <typeparamref name="T"/> uniformly
/// distributed within the specified range. Works with any type implementing
/// <see cref="INumber{TSelf}"/>, including <see cref="int"/>,
/// <see cref="long"/>, <see cref="double"/>, <see cref="decimal"/>,
/// <see cref="Half"/>, <see cref="Int128"/>, and user-defined numeric types.
/// </summary>
/// <typeparam name="T">
/// Any numeric type implementing <see cref="INumber{TSelf}"/>. The type's
/// arithmetic operations are used for range calculation and shrinking.
/// </typeparam>
/// <param name="min">
/// Inclusive lower bound. Defaults to <c>T.MinValue</c> for bounded types,
/// or a sensible default for unbounded types.
/// </param>
/// <param name="max">
/// Inclusive upper bound. Defaults to <c>T.MaxValue</c> for bounded types.
/// </param>
/// <returns>
/// A strategy producing values in [<paramref name="min"/>, <paramref name="max"/>].
/// Values shrink toward <paramref name="min"/>.
/// </returns>
public static Strategy<T> Numbers<T>(T? min = default, T? max = default)
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    T effectiveMin = min ?? T.MinValue;
    T effectiveMax = max ?? T.MaxValue;
    return new NumberStrategy<T>(effectiveMin, effectiveMax);
}
```

**Internal implementation using generic math:**

```csharp
/// <summary>
/// Strategy for generating any <see cref="INumber{T}"/> type. Uses generic
/// math interfaces for all arithmetic, avoiding per-type specialization
/// while maintaining zero boxing.
/// </summary>
internal sealed class NumberStrategy<T> : Strategy<T>
    where T : struct, INumber<T>, IMinMaxValue<T>
{
    private readonly T min;
    private readonly T max;

    internal NumberStrategy(T min, T max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min),
                $"min ({min}) must be ≤ max ({max})");
        min = min;
        max = max;
    }

    internal override T Draw(ConjectureData data)
    {
        // For integer types: draw bytes, map to range
        // For floating-point types: draw via IEEE 754 encoding
        // INumber<T> provides the arithmetic for range mapping
        if (T.IsInteger(T.Zero))
            return DrawIntegral(data);
        else
            return DrawFloatingPoint(data);
    }

    private T DrawIntegral(ConjectureData data)
    {
        // Calculate bytes needed for the range
        // T.CreateChecked converts from the raw ulong without boxing
        T range = max - min;
        int bytesNeeded = EstimateBytes(range);
        ReadOnlySpan<byte> raw = data.DrawBytes(bytesNeeded);

        if (data.Status == Status.Overrun)
            return min;

        ulong rawValue = ReadBigEndian(raw);
        // Generic math: T.CreateTruncating maps from ulong → T without boxing
        T offset = T.CreateTruncating(rawValue % ulong.CreateTruncating(range + T.One));
        return min + offset;
    }

    private T DrawFloatingPoint(ConjectureData data)
    {
        // IEEE 754 strategy: draw raw bits, interpret as T
        // Handles NaN, ±Inf, ±0, subnormals via special draw paths
        // Uses T.CreateChecked for the final conversion
        // ...
    }
}
```

**Key `INumber<T>` interfaces used:**

| Interface | Purpose in strategy | Example usage |
|---|---|---|
| `INumber<T>` | Core arithmetic (`+`, `-`, `>`, `<`) | Range calculation, shrink direction |
| `IMinMaxValue<T>` | Default bounds (`T.MinValue`, `T.MaxValue`) | Optional parameter defaults |
| `INumberBase<T>` | `T.CreateTruncating()`, `T.CreateChecked()` | Convert raw bytes → T without boxing |
| `INumberBase<T>.IsInteger()` | Dispatch integral vs floating-point | Choose draw strategy |
| `IBinaryInteger<T>` | Bit width, leading zeros | Calculate bytes needed for range |
| `IFloatingPointIeee754<T>` | NaN, Epsilon, special values | Float-specific generation |

**Convenience aliases preserve discoverability:**

```csharp
/// <summary>Shorthand for <c>Numbers&lt;int&gt;(min, max)</c>.</summary>
public static Strategy<int> Integers(int min = int.MinValue, int max = int.MaxValue)
    => Numbers<int>(min, max);

/// <summary>Shorthand for <c>Numbers&lt;long&gt;(min, max)</c>.</summary>
public static Strategy<long> Longs(long min = long.MinValue, long max = long.MaxValue)
    => Numbers<long>(min, max);

/// <summary>Shorthand for <c>Numbers&lt;double&gt;(min, max)</c>.</summary>
public static Strategy<double> Doubles(double min = double.MinValue, double max = double.MaxValue)
    => Numbers<double>(min, max);

// etc. — thin wrappers, all delegate to Numbers<T>
```

This gives users two paths: `Strategies.Numbers<Half>()` for any numeric type (including exotic ones like `Half`, `Int128`, or custom types), and `Strategies.Integers()` / `Strategies.Doubles()` as familiar shortcuts that show up first in IntelliSense.

**Boxing impact: zero.** The entire `NumberStrategy<T>` is generic over a `struct` constraint. `T.CreateTruncating(ulong)` is a static interface method — no virtual dispatch, no boxing. The JIT specializes the entire draw path per concrete type.

### 3.7 Leveraging .NET 10 & C# 14 Features

Targeting .NET 10 (LTS, supported until November 2028) unlocks a significant set of language, runtime, and JIT improvements that directly benefit a library like Conjecture. This section catalogs the features worth adopting and how they map to specific components.

#### C# 14 language features

**1. Extension members — fluent strategy composition**

C# 14's extension blocks replace the scattered `static class` extension method pattern with cohesive extension types. This is perfect for adding LINQ-style combinators to `Strategy<T>` without polluting the base class:

```csharp
public static class StrategyExtensions
{
    extension<T>(Strategy<T> strategy)
    {
        /// <summary>
        /// Filters generated values, retrying up to the rejection budget.
        /// </summary>
        public Strategy<T> Where(Func<T, bool> predicate)
            => new FilteredStrategy<T>(strategy, predicate);

        /// <summary>
        /// Transforms generated values using the given projection.
        /// </summary>
        public Strategy<U> Select<U>(Func<T, U> selector)
            => new MappedStrategy<T, U>(strategy, selector);

        /// <summary>True if the strategy can produce values (non-empty).</summary>
        public bool IsEmpty => false;
    }
}
```

Extension properties (e.g., `strategy.IsEmpty`) were previously impossible — C# 14 makes them first-class. This enables richer IntelliSense on `Strategy<T>` without inheritance.

**2. Field-backed properties — cleaner `ConjectureSettings`**

The `field` keyword eliminates manual backing fields for validated settings:

```csharp
public sealed record ConjectureSettings
{
    /// <summary>Maximum number of examples to generate before concluding.</summary>
    public int MaxExamples
    {
        get;
        init => field = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(MaxExamples));
    } = 100;

    /// <summary>Per-example deadline. Tests exceeding this are marked slow.</summary>
    public TimeSpan Deadline
    {
        get;
        init => field = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(Deadline));
    } = TimeSpan.FromMilliseconds(200);
}
```

No manual backing fields cluttering the type. The compiler generates them.

**3. Partial constructors — source generator ergonomics**

C# 14 adds `partial` constructors, extending C# 13's partial properties. The `[Arbitrary]` source generator (Section 1.5) can now emit constructor logic without owning the entire constructor:

```csharp
// User writes:
[Arbitrary]
public partial record Person(string Name, int Age);

// Source generator emits the partial constructor body:
public partial record Person
{
    // Validator emitted by the source generator
    public partial Person(string Name, int Age)
    {
        // generated validation or strategy registration logic
    }
}
```

**4. First-class `Span<T>` conversions**

C# 14 adds implicit conversions between `T[]`, `Span<T>`, and `ReadOnlySpan<T>`. This means the engine's `byte[]` buffers can be passed directly to Span-accepting methods without explicit `.AsSpan()` calls — less ceremony in the shrinker and draw paths:

```csharp
// Before C# 14: explicit conversion needed
ReadOnlySpan<byte> span = buffer.AsSpan(offset, length);

// C# 14: implicit conversion
ReadOnlySpan<byte> span = buffer[offset..(offset + length)];

// Methods accepting ReadOnlySpan<byte> now accept byte[] directly
void ProcessBlock(ReadOnlySpan<byte> data) { ... }
ProcessBlock(buffer);  // implicit conversion, no .AsSpan()
```

**5. Null-conditional assignment (`?.=`)**

Useful for optional configuration patterns:

```csharp
settings?.Database ?.= DatabaseMode.Auto;  // only assign if settings and Database aren't null
```

**6. User-defined compound assignment operators**

If `Strategy<T>` supports combination (e.g., `|` for `OneOf`), users can define `|=` for accumulating strategies:

```csharp
Strategy<int> combined = Strategies.Integers(0, 10);
combined |= Strategies.Integers(100, 200);  // OneOf(original, new)
```

#### .NET 10 JIT & runtime improvements

These are "free" performance wins — the library doesn't need to change code, but should be structured to benefit maximally.

**1. Expanded escape analysis & stack allocation**

The .NET 10 JIT significantly extends escape analysis. Objects that don't escape a method can be stack-allocated, eliminating GC pressure. This directly benefits:

- **`ConjectureData` internal helpers** — small structs and temporary objects created during draw operations that the JIT can now prove don't escape
- **Lambda/delegate closures** — the shrinker uses lambdas for shrink pass predicates. .NET 10 can stack-allocate closure objects for non-escaping delegates
- **Enumerator allocations** — iterating `List<Block>` via `foreach` previously heap-allocated the enumerator. .NET 10's array/list de-abstraction can stack-allocate these

Design implication: prefer small, short-lived objects over long-lived ones where possible. The JIT will do the rest.

**2. Improved devirtualization and inlining**

.NET 10's JIT can now devirtualize methods that become eligible after previous inlining passes, and can inline methods with `try-finally` blocks. This means:

- `Strategy<T>.Draw()` virtual calls through sealed concrete strategies (like `NumberStrategy<T>`) will be devirtualized and inlined
- The engine's `using var data = ConjectureData.Create()` pattern (which has an implicit `try-finally` for `Dispose`) can now be inlined

Design implication: mark concrete strategy classes as `sealed` (already in the plan) — this gives the JIT the information it needs to devirtualize.

**3. Better struct argument codegen**

.NET 10 eliminates unnecessary memory operations when passing struct arguments. The `Block` readonly record struct passes through registers instead of being spilled to the stack. For the shrinker iterating thousands of blocks, this reduces memory traffic.

**4. Loop inversion and block reordering**

The JIT now uses a 3-opt heuristic (modeled as an asymmetric Travelling Salesman Problem) for basic block layout. The shrinker's inner loops — which iterate over blocks and attempt mutations — get better branch prediction and instruction cache locality automatically.

**5. AVX10.2 / ARM SVE vectorization**

Not directly applicable to most of the library, but byte-buffer comparison operations in the shrinker (comparing candidate buffers against the current best) could benefit from auto-vectorized `SequenceEqual` and `CopyTo` implementations in the BCL.

**6. NativeAOT compatibility**

.NET 10 expands NativeAOT scenario support. Since the library avoids runtime reflection (source generators instead of `AutoBuild`), it should be NativeAOT-compatible out of the box. This matters for users running property tests in AOT-compiled test harnesses or CI environments where startup time matters.

Add to the CI matrix:

```xml
<!-- In test .csproj -->
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

**7. GC improvements — DATAS enabled by default**

.NET 10 enables Dynamic Adaptation To Application Sizes (DATAS) by default, which auto-tunes heap sizing. For property tests that generate and discard thousands of short-lived objects, this reduces GC pause variance without configuration.

#### Summary: feature → component mapping

| Feature | Component | Benefit |
|---|---|---|
| Extension members (C# 14) | `Strategy<T>` combinators | Extension properties, cleaner API surface |
| `field` keyword (C# 14) | `ConjectureSettings`, strategy config types | Less boilerplate, validated properties |
| Partial constructors (C# 14) | `[Arbitrary]` source generator | Emit constructor logic without owning the type |
| Implicit Span conversions (C# 14) | `ConjectureData`, shrinker | Less ceremony passing buffers |
| Escape analysis (JIT) | Engine hot loop, shrinker closures | Automatic stack allocation of temporaries |
| Devirtualization (JIT) | `Strategy<T>.Draw()` dispatch | Sealed strategies inline to direct calls |
| Struct codegen (JIT) | `Block` iteration in shrinker | Register-based passing, less memory traffic |
| Loop optimization (JIT) | Shrinker inner loops | Better branch prediction, cache locality |
| NativeAOT (.NET 10) | CI, user test harnesses | AOT-compatible by design (no reflection) |
| DATAS GC (.NET 10) | Entire test execution | Auto-tuned heap sizing for burst allocation patterns |

---

## 4. Phased Implementation Plan

### Phase 0: Foundation & Proof of Concept (4–6 weeks)

**Goal:** A working Conjecture engine that can generate, run, and shrink a single integer-based property test.

#### 0.1 Project scaffolding
- Solution structure:
  - `Hypothesis.Core` — engine, strategies, data, settings
  - `Hypothesis.Generators` — incremental source generator (`IIncrementalGenerator`) for `[Arbitrary]` types
  - `Hypothesis.Analyzers` — Roslyn analyzers for property test quality (see Section 6.5)
  - `Hypothesis.Xunit` — xUnit integration
  - `Hypothesis.FSharp` — F# idiomatic API: `Gen` module, `gen { }` computation expression, `Gen.auto<'T>` (see Section 3.6)
  - `Hypothesis.FSharp.Expecto` — Expecto test framework integration
  - `Hypothesis.Tests` — internal unit test suite
  - `Conjecture.SelfTests` — property tests that use Conjecture to test itself (see Section 0.7)
  - `Hypothesis.Benchmarks` — BenchmarkDotNet suite
- CI pipeline (GitHub Actions): build, test, pack NuGet
- Adopt `Nerdbank.GitVersioning` or `MinVer` for semver

#### 0.2 Choice sequence engine (`ConjectureData`)
This is the heart of Hypothesis. Everything else builds on it. See **Section 3.3** for the full memory model using `Span<byte>`, `ArrayPool`, and zero-allocation design.

```csharp
public sealed class ConjectureData : IDisposable
{
    private byte[] rentedBuffer;       // pooled from ArrayPool<byte>.Shared
    private int length;                // logical buffer length
    private int index;                 // current read position
    private readonly List<Block> blocks;   // block boundaries for shrinking

    // Core drawing primitives — Span-based, zero-copy
    public ReadOnlySpan<byte> DrawBytes(int count);
    public ulong DrawInteger(ulong min, ulong max);
    public bool DrawBoolean();
    public void MarkInteresting(string label);
    public void Assume(bool condition);     // reject if false
    
    // Status tracking
    public Status Status { get; }           // Valid, Invalid, Interesting, Overrun
    
    // Buffer access — no allocation
    public ReadOnlySpan<byte> ConsumedBytes { get; }
    public ReadOnlySpan<byte> RemainingBytes { get; }
    
    public void Dispose();  // returns buffer to ArrayPool
}

public enum Status { Valid, Invalid, Interesting, Overrun }
```

Key implementation details:
- The buffer is rented from `ArrayPool<byte>.Shared` — critical for shrinking performance where thousands of buffers may be created and discarded
- Each "draw" call reads from the buffer via `Span<byte>` (zero-copy) and records a `Block` (start index, length, semantic label)
- Blocks enable structured shrinking (the shrinker operates on blocks, not raw bytes)
- `IDisposable` returns the buffer to the pool — use `using` in the engine loop

#### 0.3 Minimal shrinker (v1)
Implement the basic shrinking passes from Hypothesis's Conjecture shrinker:

1. **Lexicographic minimize** — try reducing each block's bytes toward zero
2. **Delete blocks** — try removing entire blocks
3. **Zero blocks** — try zeroing entire blocks
4. **Reduce integer ranges** — binary search toward the minimum

Skip advanced passes (adaptive, swapping, redistributing) for Phase 0. The basic passes alone are already better than most existing .NET PBT shrinkers.

#### 0.4 Test runner loop
The core test loop:

```
1. Generate a random byte buffer
2. Execute the test function, which draws from the buffer via ConjectureData
3. If test passes → record as Valid, try next buffer
4. If test fails → record as Interesting, enter shrinking
5. Shrink: mutate the buffer, re-run, keep smallest Interesting buffer
6. Report the minimal failing example
```

#### 0.5 Primitive strategies
- `Strategies.Booleans()`
- `Strategies.Numbers<T>(T min, T max)` — unified numeric strategy via `INumber<T>` (see Section 3.6)
- `Strategies.Integers(int min, int max)` — convenience alias for `Numbers<int>`
- `Strategies.Bytes()`
- `Strategies.Floats()` (basic — no special NaN/infinity handling yet)

#### 0.6 Proof-of-concept integration
A minimal xUnit `[Property]` attribute that:
- Discovers test methods with `Strategy<T>` parameters
- Runs the Conjecture loop
- Reports failures with the shrunk counterexample

**Phase 0 exit criteria:** Can run `[Property] void SortIsIdempotent(List<int> xs)` end-to-end with shrinking.

#### 0.7 Self-testing ("eating our own dogfood")

Hypothesis's own test suite uses `@given` extensively to property-test its internals — the Conjecture engine, shrinker, strategies, and database. Conjecture should adopt this practice from Phase 0 onward. As soon as the engine can run a basic property test, it should start testing itself.

This is both a correctness strategy and a marketing asset: a testing library that tests itself with itself is the strongest credibility signal an open-source project can send.

**Phase 0 self-tests (bootstrap):**

```csharp
// The engine is barely alive — use it to test its own primitives

/// <summary>
/// Any integer drawn with bounds must respect those bounds.
/// </summary>
[Property]
public void IntegersRespectBounds(int min, int max)
{
    Assume.That(min <= max);
    var strategy = Strategies.Integers(min, max);
    // Use Conjecture to test Conjecture
    var result = PropertyRunner.Run(
        strategy,
        value => Assert.InRange(value, min, max),
        new ConjectureSettings { MaxExamples = 50 });
    Assert.Equal(PropertyStatus.Passed, result.Status);
}

/// <summary>
/// A shrunk buffer is always lexicographically ≤ the original.
/// </summary>
[Property]
public void ShrinkingAlwaysReduces(byte[] buffer)
{
    Assume.That(buffer.Length > 0);
    var shrunk = Shrinker.Minimize(buffer, _ => true);
    Assert.True(
        shrunk.AsSpan().SequenceCompareTo(buffer) <= 0,
        "Shrunk buffer must be ≤ original");
}

/// <summary>
/// Shrinking is idempotent — shrinking an already-shrunk buffer
/// produces the same buffer.
/// </summary>
[Property]
public void ShrinkingIsIdempotent(byte[] buffer)
{
    Assume.That(buffer.Length > 0);
    Func<byte[], bool> predicate = _ => true;
    var once = Shrinker.Minimize(buffer, predicate);
    var twice = Shrinker.Minimize(once, predicate);
    Assert.Equal(once, twice);
}
```

**Phase 1 self-tests (strategy correctness):**

```csharp
/// <summary>
/// Select with the identity function produces the same values.
/// (Functor identity law)
/// </summary>
[Property]
public void SelectIdentityLaw(int value)
{
    var strategy = Strategies.Just(value).Select(x => x);
    var drawn = DrawOnce(strategy);
    Assert.Equal(value, drawn);
}

/// <summary>
/// Where(x => true) never rejects — every draw succeeds.
/// </summary>
[Property]
public void WhereTrueNeverRejects(int value)
{
    var strategy = Strategies.Integers().Where(_ => true);
    var result = PropertyRunner.Run(
        strategy,
        _ => { },
        new ConjectureSettings { MaxExamples = 50 });
    Assert.Equal(PropertyStatus.Passed, result.Status);
}

/// <summary>
/// SelectMany associativity: binding f then g is the same as
/// binding the composition.
/// </summary>
[Property]
public void SelectManyAssociativity(int seed)
{
    Strategy<int> addOne = Strategies.Just(seed)
        .SelectMany(x => Strategies.Just(x + 1));
    Strategy<int> addTwo = Strategies.Just(seed)
        .SelectMany(x => Strategies.Just(x + 1))
        .SelectMany(x => Strategies.Just(x + 1));

    Assert.Equal(seed + 2, DrawOnce(addTwo));
}
```

**Phase 2 self-tests (database & infrastructure):**

```csharp
/// <summary>
/// Database roundtrip: save a buffer, load it back, get the same bytes.
/// </summary>
[Property]
public void DatabaseRoundtrips(string key, byte[] buffer)
{
    Assume.That(!string.IsNullOrEmpty(key));
    Assume.That(buffer.Length > 0);

    using var db = new InMemoryDatabase();
    db.SaveAsync(key, buffer).GetAwaiter().GetResult();
    var loaded = db.LoadAsync(key).GetAwaiter().GetResult();
    Assert.Equal(buffer, loaded);
}

/// <summary>
/// Shrinking preserves Interesting status — if the original buffer
/// causes a failure, the shrunk buffer must also cause the same failure.
/// </summary>
[Property]
public void ShrinkingPreservesInteresting(List<int> xs)
{
    Assume.That(xs.Count > 0);
    // The test "list is empty" always fails for non-empty lists.
    // After shrinking, the minimal failing example should be a
    // single-element list — still non-empty, still failing.
    var result = PropertyRunner.Run(
        Strategies.Lists(Strategies.Integers(), minSize: 1),
        list => Assert.Empty(list));

    Assert.Equal(PropertyStatus.Failed, result.Status);
    Assert.Single(result.MinimalCounterexample as IList<int>);
}
```

**Self-testing growth by phase:**

| Phase | Self-test focus | Example properties |
|---|---|---|
| Phase 0 | Engine basics | Bounds respected, shrinking reduces, shrinking is idempotent |
| Phase 1 | Strategy laws | Functor identity, filter true/false, SelectMany associativity, collection size bounds |
| Phase 2 | Infrastructure | Database roundtrips, shrinking preserves status, settings validation, reporting accuracy |
| Phase 3 | Stateful testing | State machine step sequences shrink correctly, rule preconditions respected, invariants hold after every step |

The self-test suite should be a separate test project (`Conjecture.SelfTests`) that depends on `Hypothesis.Xunit` — running the library's own `[Property]` attribute against its own internals. This creates a virtuous cycle: any regression in the engine breaks the self-tests, and improving the engine makes the self-tests more powerful.

---

### Phase 1: Core Strategy Library (6–8 weeks)

**Goal:** A comprehensive set of built-in strategies covering most common C# types.

#### 1.1 Strategy combinators
These mirror LINQ and are the composition backbone:

```csharp
public abstract class Strategy<T> : IStrategyMarker
{
    /// <summary>
    /// Produces a single value from this strategy. This is the public entry point
    /// for manual strategy usage outside of <c>[Property]</c> tests.
    /// </summary>
    /// <returns>A generated value of type <typeparamref name="T"/>.</returns>
    public T Next() => Next(ConjectureSettings.Default);

    /// <summary>
    /// Produces a single value from this strategy using the given settings.
    /// </summary>
    public T Next(ConjectureSettings settings)
    {
        using var data = ConjectureData.Create();
        FillBuffer(data, new Random(settings.Seed ?? Random.Shared.Next()));
        return Draw(data);
    }

    /// <summary>
    /// Internal draw method called by the Conjecture engine during test execution.
    /// Named "Draw" to match Hypothesis Python's terminology for easier
    /// cross-referencing with the upstream implementation.
    /// </summary>
    /// <remarks>
    /// Contributors porting logic from Python Hypothesis should map
    /// <c>data.draw(strategy)</c> → <c>strategy.Draw(data)</c>.
    /// End users never call this directly — use <see cref="Next()"/> or
    /// <c>[Property]</c> tests instead.
    /// </remarks>
    internal abstract T Draw(ConjectureData data);
    
    // LINQ-compatible combinators
    public Strategy<U> Select<U>(Func<T, U> map);
    public Strategy<T> Where(Func<T, bool> predicate);  // with rejection sampling + budgets
    public Strategy<U> SelectMany<U>(Func<T, Strategy<U>> bind);
    
    // Convenience
    public Strategy<T> WithLabel(string label);  // for shrink reporting
    public Strategy<(T, U)> Zip<U>(Strategy<U> other);
}
```

#### 1.2 Primitive strategies (complete set)
- **Numeric (unified via `INumber<T>`):** Single `Numbers<T>()` entry point (see Section 3.6), with convenience aliases:
  - `Integers()`, `Longs()`, `Doubles()`, `Floats()`, `Decimals()`
  - Also supports `Half`, `Int128`, `UInt128`, `BigInteger`, and any user-defined `INumber<T>`
  - With range constraints, special float values (NaN, ±Inf, ±0, subnormals) via `IFloatingPointIeee754<T>`
- **Text:** `Strings`, `Chars`, `StringsOf(alphabet)`, `UnicodeStrings`, `AsciiStrings`
  - Alphabet-constrained generation
  - Length bounds
- **Bytes:** `Bytes`, `ByteArrays`
- **Temporal:** `DateTimes`, `DateTimeOffsets`, `TimeSpans`, `DateOnly`, `TimeOnly`
- **Boolean:** `Booleans`
- **Enums:** `Enums<T>()` (auto-discovers values)

#### 1.3 Collection strategies
- `Lists<T>(Strategy<T>, minSize, maxSize)`
- `Arrays<T>(...)`
- `Dictionaries<K,V>(...)`
- `Sets<T>(...)`
- `SortedLists`, `Queues`, `Stacks`, etc.
- All with configurable size bounds
- `UniqueBy` variant for distinct-element collections

#### 1.4 Composite strategies
- `OneOf(params Strategy<T>[] strategies)` — weighted choice
- `Just(T value)` — constant strategy
- `None<T>()` — always rejects (for completeness)
- `Nullable<T>(Strategy<T>)` — nullable wrapper
- `Tuples(Strategy<T1>, Strategy<T2>, ...)` — up to 8-arity

#### 1.5 Imperative strategy composition (`Strategies.Compose`)

Python Hypothesis's `@composite` decorator is one of its most-used features — it lets you build strategies imperatively, where each drawn value can depend on previous results. The plan already supports the functional equivalent via `SelectMany`, but that becomes unreadable beyond 2–3 dependent values. Conjecture provides three tiers of composition, matching increasing complexity:

**Tier 1 — Simple: no dependencies**

```csharp
var ages = Strategies.Integers(0, 150);
```

**Tier 2 — Medium: LINQ query syntax (2–4 dependent values)**

Because `Strategy<T>` defines `Select` and `SelectMany`, LINQ comprehensions work automatically:

```csharp
// Generates sorted pairs where y >= x
var sortedPairs =
    from x in Strategies.Integers()
    from y in Strategies.Integers(min: x)
    select (x, y);

// Generates HTTP requests with conditional body
var httpRequests =
    from method in Strategies.OneOf("GET", "POST", "PUT")
    from path in Strategies.Strings(minSize: 1).Select(s => "/" + s)
    from body in method != "GET"
        ? Strategies.ByteArrays()
        : Strategies.Just<byte[]?>(null)
    from headers in Strategies.Dictionaries(
        Strategies.Strings(), Strategies.Strings())
    select new HttpRequest(method, path, body, headers);
```

This is clean and idiomatic C#. However, it can't express loops, early `Assume.That()` calls, or branching logic mid-composition.

**Tier 3 — Complex: imperative `Strategies.Compose` (Python `@composite` equivalent)**

```csharp
/// <summary>
/// Builds a strategy using an imperative callback where each
/// <see cref="IGeneratorContext.Next{T}"/> call can depend on
/// the results of previous calls. This is the C# equivalent of
/// Python Hypothesis's <c>@composite</c> decorator.
/// </summary>
/// <typeparam name="T">The type of value the strategy produces.</typeparam>
/// <param name="builder">
/// A function that receives a <see cref="IGeneratorContext"/> and uses it
/// to generate dependent values imperatively. Each <c>Next</c> call
/// maps to a block in the underlying byte stream, so shrinking works
/// automatically.
/// </param>
/// <returns>A strategy that produces values by executing the builder.</returns>
/// <example>
/// <code>
/// var strategy = Strategies.Compose&lt;HttpRequest&gt;(gen =>
/// {
///     var method = gen.Next(Strategies.OneOf("GET", "POST", "PUT"));
///     var path = gen.Next(Strategies.Strings(minSize: 1));
///     byte[]? body = method != "GET"
///         ? gen.Next(Strategies.ByteArrays())
///         : null;
///     return new HttpRequest(method, path, body);
/// });
/// </code>
/// </example>
public static Strategy<T> Compose<T>(Func<IGeneratorContext, T> builder)
```

**The `IGeneratorContext` interface:**

```csharp
/// <summary>
/// Provides imperative access to strategy generation within a
/// <see cref="Strategies.Compose{T}"/> callback. Each call to
/// <see cref="Next{T}"/> reads from the underlying byte stream,
/// enabling fully shrinkable dependent value generation.
/// </summary>
public interface IGeneratorContext
{
    /// <summary>
    /// Generates a value from the given strategy. The result can be used
    /// to configure subsequent strategy calls, enabling dependent generation.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="strategy">The strategy to generate from.</param>
    /// <param name="label">
    /// Optional label for diagnostics and shrink reporting.
    /// </param>
    /// <returns>A generated value of type <typeparamref name="T"/>.</returns>
    T Next<T>(Strategy<T> strategy, string? label = null);
}
```

**Full example — complex domain object with loops and conditionals:**

```csharp
var validOrders = Strategies.Compose<Order>(gen =>
{
    var customerId = gen.Next(Strategies.Guids(), label: "customerId");
    var itemCount = gen.Next(Strategies.Integers(1, 20), label: "itemCount");

    var items = new List<OrderItem>();
    for (int i = 0; i < itemCount; i++)
    {
        var sku = gen.Next(Strategies.StringsOf(
            Strategies.Chars('A', 'Z'), minLength: 3, maxLength: 8));
        var quantity = gen.Next(Strategies.Integers(1, 100));
        var unitPrice = gen.Next(
            Strategies.Numbers<decimal>(0.01m, 9999.99m));

        items.Add(new OrderItem(sku, quantity, unitPrice));
    }

    Assume.That(items.Sum(i => i.Quantity * i.UnitPrice) < 100_000m);

    var discount = gen.Next(Strategies.Numbers<decimal>(0m, 0.3m));

    return new Order(customerId, items, discount);
});
```

This enables:
- **Loops** — generate a variable number of items, each depending on a drawn count
- **Mid-composition filtering** — `Assume.That()` can reject after partial generation
- **Complex branching** — full `if`/`switch` logic based on previously drawn values
- **Labels** — optional diagnostic labels on each `Next` call for clearer shrink output

**Internal implementation:**

```csharp
internal sealed class ComposedStrategy<T> : Strategy<T>
{
    private readonly Func<IGeneratorContext, T> builder;

    internal ComposedStrategy(Func<IGeneratorContext, T> builder)
    {
        this.builder = builder;
    }

    internal override T Draw(ConjectureData data)
    {
        var context = new GeneratorContext(data);
        return builder(context);
    }

    private sealed class GeneratorContext : IGeneratorContext
    {
        private readonly ConjectureData data;

        public GeneratorContext(ConjectureData data)
        {
            this.data = data;
        }

        public TValue Next<TValue>(Strategy<TValue> strategy, string? label = null)
        {
            if (label != null)
                data.PushLabel(label);

            var value = strategy.Draw(data);

            if (label != null)
                data.PopLabel();

            return value;
        }
    }
}
```

The `GeneratorContext` is a thin wrapper around `ConjectureData`. Each `Next` call delegates to the strategy's internal `Draw` method, reading from the same byte stream. Since every drawn value maps to blocks in the buffer, shrinking works automatically — the shrinker doesn't know or care whether blocks were drawn via LINQ composition or imperative `Compose`.

**Comparison of approaches:**

| Aspect | `SelectMany` chain | LINQ query syntax | `Strategies.Compose` |
|---|---|---|---|
| Dependencies between values | Yes | Yes | Yes |
| Readability at 2 values | Good | Great | Acceptable |
| Readability at 4+ values | Poor (deep nesting) | Good | Great |
| Loops / variable-count generation | No | No | Yes |
| Mid-composition `Assume.That()` | No | No | Yes |
| Complex branching | Limited | Limited | Full |
| Shrinking | Automatic | Automatic | Automatic |
| Performance | Zero overhead | Zero overhead | One allocation (`GeneratorContext`) |

#### 1.6 Source-generated object building

Instead of reflection-based `AutoBuild<T>`, Conjecture uses an **incremental source generator** (`IIncrementalGenerator`) as the primary mechanism for deriving strategies from user types. This runs at compile time — zero runtime reflection, full AOT compatibility, and compile-time diagnostics for unsupported types.

**Project structure:**

```
Hypothesis.Core            — engine, strategies, runtime
Hypothesis.Generators      — IIncrementalGenerator (analyzer/source gen assembly)
Hypothesis.Xunit           — test framework integration
```

The source generator ships as an analyzer dependency in the main NuGet package, so users get it automatically without extra setup.

**User experience — `[Arbitrary]` attribute:**

```csharp
// Step 1: Mark the type as partial + [Arbitrary]
[Arbitrary]
public partial record Person(string Name, int Age, string? Email);

// Step 2: That's it. The source generator emits a Strategy<Person>.
// Usage in tests:
[Property]
public void PersonNameIsNeverNull(Person p)
    => Assert.NotNull(p.Name);

// Or reference the generated strategy explicitly:
Strategy<Person> people = PersonStrategyExtensions.GetStrategy();
```

**What the source generator emits:**

```csharp
// Auto-generated — do not edit
// <auto-generated/>

#nullable enable

namespace MyApp.Generated
{
    /// <summary>
    /// Source-generated strategy for <see cref="Person"/>.
    /// Produces instances by generating each constructor parameter independently.
    /// </summary>
    /// <remarks>
    /// <para>Generated from constructor: <c>Person(string Name, int Age, string? Email)</c></para>
    /// <para>Parameter strategies:</para>
    /// <list type="bullet">
    /// <item><c>Name</c> → <see cref="Strategies.Strings()"/></item>
    /// <item><c>Age</c> → <see cref="Strategies.Numbers{int}()"/></item>
    /// <item><c>Email</c> → <see cref="Strategies.Nullable(Strategies.Strings())"/></item>
    /// </list>
    /// </remarks>
    internal sealed class PersonStrategy : Strategy<Person>
    {
        private readonly Strategy<string> nameStrategy = Strategies.Strings();
        private readonly Strategy<int> ageStrategy = Strategies.Numbers<int>();
        private readonly Strategy<string?> emailStrategy = Strategies.Nullable(Strategies.Strings());

        internal override Person Draw(ConjectureData data)
        {
            var name = nameStrategy.Draw(data);
            var age = ageStrategy.Draw(data);
            var email = emailStrategy.Draw(data);
            return new Person(name, age, email);
        }
    }

    /// <summary>
    /// Registers <see cref="PersonStrategy"/> so that <c>[Property]</c> tests
    /// with <see cref="Person"/> parameters automatically use it.
    /// </summary>
    internal static class PersonStrategyExtensions
    {
        [ModuleInitializer]
        internal static void Register()
            => StrategyRegistry.Register<Person>(new PersonStrategy());

        public static Strategy<Person> GetStrategy() => new PersonStrategy();
    }
}
```

**Source generator design decisions:**

| Decision | Choice | Rationale |
|---|---|---|
| Generator kind | `IIncrementalGenerator` | Incremental — only re-runs when the type changes, fast IDE experience |
| Constructor selection | Primary constructor preferred, then single public constructor, then diagnostic error | Predictable, matches `record` convention |
| Parameter → strategy mapping | Type-based lookup at compile time | Uses known mappings: `string` → `Strings()`, `int` → `Numbers<int>()`, `T?` → `Nullable(For<T>())`, `List<T>` → `Lists(For<T>())`, etc. |
| Unsupported parameter type | Emit `CON001` compiler diagnostic | "No default strategy for type 'Foo'. Add [Arbitrary] to Foo or use [StrategyOverride] on the parameter." |
| Nested `[Arbitrary]` types | Follow the reference | If `Person` has an `Address` field also marked `[Arbitrary]`, use the generated `AddressStrategy` |
| Registration | `[ModuleInitializer]` | Auto-registers on assembly load — zero manual wiring |
| `partial` requirement | Required | Source generators can only add members to `partial` types |

**Strategy overrides for individual properties:**

When the default mapping isn't right (e.g., `Age` should be 0–150, not `int.MinValue`–`int.MaxValue`):

```csharp
[Arbitrary]
public partial record Person(
    string Name,
    [StrategyOverride<ValidAge>] int Age,   // uses custom strategy
    string? Email);

// ValidAge is a NumberStrategy<int> subclass from Section 3.6
public sealed class ValidAge : NumberStrategy<int>
{
    public ValidAge() : base(min: 0, max: 150) { }
}
```

The source generator reads `[StrategyOverride<T>]` and emits `new ValidAge()` instead of `Strategies.Numbers<int>()` for that parameter.

**Fluent builder as a runtime alternative:**

For cases where source generation isn't viable (dynamic types, types in external assemblies you can't mark `partial`), provide a fluent builder that uses compiled expression trees — not runtime reflection:

```csharp
/// <summary>
/// Builds a strategy for <typeparamref name="T"/> using a fluent API.
/// Internally compiles expression trees to delegates on first use —
/// subsequent draws have no reflection overhead.
/// </summary>
/// <remarks>
/// Prefer <c>[Arbitrary]</c> source generation for types you control.
/// Use <see cref="Builds{T}"/> for external types or when dynamic
/// configuration is needed.
/// </remarks>
public static StrategyBuilder<T> Builds<T>() where T : class
    => new StrategyBuilder<T>();

// Usage:
var strategy = Strategies.Builds<ExternalPerson>()
    .Via(ctor: () => new ExternalPerson("", 0, null))  // constructor shape
    .With(p => p.Name, Strategies.Strings())
    .With(p => p.Age, Strategies.Numbers<int>(0, 150))
    .With(p => p.Email, Strategies.Nullable(Strategies.Strings()))
    .Compile();  // compiles to delegate — no reflection after this point
```

`Compile()` uses `Expression.Lambda<Func<...>>().Compile()` to emit a delegate once, cached and reused on every subsequent draw.

**Comparison of approaches:**

| Aspect | `[Arbitrary]` source gen | `Builds<T>()` fluent | Reflection `AutoBuild` (removed) |
|---|---|---|---|
| Runtime overhead | Zero — compiled in | One-time expression compile | Every draw — `Activator.CreateInstance` |
| AOT compatible | Yes | Yes (compiled expressions) | No — runtime reflection |
| Compile-time errors | Yes — diagnostics for unsupported types | No — runtime errors | No — runtime errors |
| IDE support | Full — IntelliSense on generated code | Partial — lambda expressions | None |
| Works on external types | No — needs `partial` | Yes | Yes |
| Boxing risk | None | None (expression-compiled) | High — `object[]` parameter arrays |

#### 1.7 Filter budgets and `Assume`
- When `.Where()` rejects too many candidates, throw `Unsatisfied` (matching Hypothesis behavior)
- Default budget: 5 draws per acceptance (configurable)
- `Assume.That(condition)` — soft rejection at the test level

---

### Phase 2: Robust Shrinker & Infrastructure (6–8 weeks)

**Goal:** A production-quality shrinker, persistent database, settings system, and polished xUnit integration.

#### 2.1 Full shrinker implementation
Port the remaining shrink passes from Hypothesis's `engine.py`:

1. **Adaptive passes** — learn which byte offsets are "interesting" and focus effort there
2. **Block swapping** — swap adjacent blocks to find simpler orderings
3. **Redistribution** — redistribute values between related blocks (e.g., list lengths)
4. **Interval deletion** — delete contiguous runs of blocks
5. **Float simplification** — special handling for floating point (lexicographic float ordering)
6. **String-aware passes** — understand character boundaries for better text shrinking

Shrinker quality is what separates Hypothesis from other PBT libraries. This phase is critical.

#### 2.2 Example database

```csharp
public interface IExampleDatabase
{
    Task SaveAsync(string key, byte[] buffer);
    Task<byte[]?> LoadAsync(string key);
    Task DeleteAsync(string key);
    IAsyncEnumerable<byte[]> FetchAllAsync(string key);
}

// Built-in implementations:
// - DirectoryBasedDatabase (default — one file per test, matching Hypothesis)
// - InMemoryDatabase (for CI/ephemeral usage)
// - SQLiteDatabase (optional package)
```

- The database key is derived from the test's fully-qualified name
- On test start, replay all known failing examples before generating new ones
- On failure, save the shrunk buffer
- On pass (for a previously-failing example), remove it

#### 2.3 Settings system

```csharp
public sealed record ConjectureSettings
{
    public int MaxExamples { get; init; } = 100;
    public TimeSpan Deadline { get; init; } = TimeSpan.FromMilliseconds(200);
    public int MaxShrinks { get; init; } = 500;
    public DatabaseMode Database { get; init; } = DatabaseMode.Auto;
    public Verbosity Verbosity { get; init; } = Verbosity.Normal;
    public ShrinkPhase[] SuppressShrinkPasses { get; init; } = [];
    public ulong? Seed { get; init; }  // explicit seed for reproduction
    
    // Profiles
    public static ConjectureSettings CI => new() { MaxExamples = 200, Deadline = ... };
    public static ConjectureSettings Debug => new() { MaxExamples = 10, Verbosity = ... };
}
```

Settings can be set via:
- `[Property(MaxExamples = 200)]` attribute
- `ConjectureSettings.Default` global override
- Named profiles loaded from config
- Environment variables (for CI)

#### 2.4 xUnit integration (polished)

```csharp
// Built-in reusable strategy types for common constraints
public sealed class PositiveInts : NumberStrategy<int>
{
    public PositiveInts() : base(min: 1) { }
}

public sealed class Percentages : NumberStrategy<double>
{
    public Percentages() : base(min: 0.0, max: 100.0) { }
}

// Basic usage — type-inferred strategies
public class MyTests
{
    [Property]
    public void ReverseReverseIsIdentity(List<int> xs)
    {
        var reversed = xs.AsEnumerable().Reverse().Reverse().ToList();
        Assert.Equal(xs, reversed);
    }
    
    // Generic attributes — compile-time type safety, full IntelliSense
    [Property]
    public void PositiveMultiplicationCommutes(
        [From<PositiveInts>] int a,
        [From<PositiveInts>] int b)
    {
        Assert.Equal(a * b, b * a);
    }
    
    // Factory method fallback for one-off strategies
    [Property]
    public void EvenNumbersAreDivisibleByTwo(
        [FromFactory(nameof(EvenInts))] int n)
    {
        Assert.Equal(0, n % 2);
    }
    
    static Strategy<int> EvenInts()
        => Strategies.Integers(0, 1000).Where(n => n % 2 == 0);
    
    // Explicit examples run first
    [Property]
    [Example(0, 0)]
    [Example(int.MaxValue, 1)]
    public void AdditionIsCommutative(int a, int b)
    {
        Assert.Equal(a + b, b + a);
    }
    
    // Async support
    [Property]
    public async Task DatabaseRoundTrips(string key, byte[] value)
    {
        await db.SetAsync(key, value);
        var result = await db.GetAsync(key);
        Assert.Equal(value, result);
    }
    
    // INumber<T> — works with any numeric type
    [Property]
    public void AdditionIsCommutative_Half(
        [From<SmallHalfs>] Half a,
        [From<SmallHalfs>] Half b)
    {
        Assert.Equal(a + b, b + a);
    }
}

// Custom strategy types are trivial one-liners
public sealed class SmallHalfs : NumberStrategy<Half>
{
    public SmallHalfs() : base(
        min: Half.CreateTruncating(-100),
        max: Half.CreateTruncating(100)) { }
}
```

Implementation via `IXunitTestCaseDiscoverer` and `XunitTestCase`:
- Discover `[Property]` methods
- Infer `Strategy<T>` for each parameter based on type (auto-registration via `StrategyRegistry`)
- Allow override via `[From<TStrategy>]` generic attribute (compile-time safe) or `[FromFactory]` for inline factories
- Display counterexamples in test output
- Integrate with xUnit's `ITestOutputHelper`

#### 2.5 Reporting, failure output, and developer experience

Failure output is where users form their first impression. A poorly formatted counterexample will make people abandon the library regardless of how good the shrinker is. This deserves careful design.

**Standard failure output format:**

```
Conjecture found a failing example after 23 attempts (shrunk 14 times):

  Falsifying example:
    xs = [3, -1]

  Minimal counterexample:
    xs = [1, 0]

  Seed: 0xA7F3B2E1
  To reproduce: [Property(Seed = 0xA7F3B2E1)]

  System.InvalidOperationException: Sequence contains no elements
    at System.Linq.ThrowHelper.ThrowNoElementsException()
    at MyTests.SortAndFilter(List<int> xs) in MyTests.cs:line 42
```

Key elements:
- **Falsifying example** — the first input that caused the failure (before shrinking)
- **Minimal counterexample** — the shrunk result (the one that matters)
- **Seed** — for deterministic reproduction, with a copy-pasteable attribute
- **Stack trace** — trimmed to remove Hypothesis internals, showing only user code

**Custom type formatting via `IStrategyFormatter<T>`:**

Complex objects need readable output. Default `ToString()` is often useless (`MyApp.Models.Order`). Provide a formatting extension point:

```csharp
/// <summary>
/// Controls how values of type <typeparamref name="T"/> are displayed
/// in failure output. Register via <see cref="FormatterRegistry"/>.
/// </summary>
public interface IStrategyFormatter<in T>
{
    /// <summary>
    /// Formats a value for display in counterexample output.
    /// Should produce readable, copy-pasteable C# where possible.
    /// </summary>
    string Format(T value);
}

// Built-in formatters produce C#-like syntax:
// int          → 42
// string       → "hello world"
// List<int>    → [3, -1, 0]
// byte[]       → new byte[] { 0x0A, 0xFF }
// null         → null
// record types → new Person("Kim", 30, null)
```

Register custom formatters for domain types:

```csharp
FormatterRegistry.Register<Order>(order =>
    $"new Order(CustomerId: {order.CustomerId}, " +
    $"Items: [{string.Join(", ", order.Items)}], " +
    $"Discount: {order.Discount}m)");
```

Goal: counterexample output should be copy-pasteable into a unit test as a regression case.

**Stateful test failure output:**

For `StateMachine` failures, print the step sequence (matching Python Hypothesis):

```
Conjecture found a failing sequence after 45 steps (shrunk to 3 steps):

  state = new DatabaseStateMachine();
  state.Init();
  state.InsertItem(key: "", value: 0);
  state.DeleteItem(key: "");
  state.ValuesAgree(key: "");  // ← fails here

  System.Xunit.Sdk.EqualException: Assert.Equal() Failure
```

**Additional diagnostics:**
- **Statistics:** `Hypothesis.Collect(label)` for distribution tracking (like Hypothesis's `note`/`event`)
- **Verbose mode:** Log each attempt, shrink step, and decision via `ITestOutputHelper`
- **Health checks:** Warn on too-high filter rejection rates, slow tests, flaky behavior
- **Shrink summary:** In verbose mode, show shrink pass breakdown (e.g., "ZeroBlocks: 5 improvements, LexMin: 2 improvements, Delete: 0")

#### 2.6 Trimming and NativeAOT compatibility

Since the library avoids runtime reflection (source generators for object building, generic `Holder<T>` for the registry), it should be trim-safe and NativeAOT-compatible by design. However, this needs explicit validation and annotation.

**Trimming annotations:**

```csharp
// Mark the public API surface as trim-safe
[assembly: AssemblyMetadata("IsTrimmable", "True")]

// Any method that could trigger reflection (e.g., type name formatting
// in diagnostics) gets annotated:
/// <summary>
/// Formats a type name for diagnostic output.
/// </summary>
[RequiresUnreferencedCode(
    "Type name formatting uses reflection for generic type display. " +
    "Use the overload accepting a pre-formatted string in trimmed contexts.")]
internal static string FormatTypeName(Type type) { ... }

// The trim-safe alternative:
internal static string FormatTypeName<T>() => typeof(T).Name;
```

**Validation in CI:**

```xml
<!-- In Hypothesis.Tests.csproj and Conjecture.SelfTests.csproj -->
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>full</TrimMode>
  <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
</PropertyGroup>
```

Add a CI job that publishes with trimming enabled and runs the full test suite against the trimmed output. Any trim warning is a build failure.

**NativeAOT test matrix:**

```yaml
# GitHub Actions
- name: Test with NativeAOT
  run: |
    dotnet publish Conjecture.SelfTests -c Release \
      -r linux-x64 --self-contained \
      -p:PublishAot=true
    ./Conjecture.SelfTests/bin/Release/net10.0/linux-x64/publish/Conjecture.SelfTests
```

**Known trim/AOT risk areas:**
- `FormatterRegistry` — if using `Dictionary<Type, object>` internally, needs `[DynamicallyAccessedMembers]`. Prefer the generic `Holder<T>` pattern (already in the plan).
- Diagnostic type name formatting — `Type.FullName` and generic argument enumeration use reflection. Provide trim-safe alternatives using `nameof` and source-generated type names.
- `Builds<T>()` fluent fallback — uses compiled expressions, which are AOT-compatible, but the expression tree construction itself needs testing under NativeAOT.

---

### Phase 3: Advanced Features (8–12 weeks)

#### 3.1 Stateful testing

The crown jewel of Hypothesis's advanced features. Port `RuleBasedStateMachine`:

```csharp
public class DatabaseStateMachine : StateMachine<DatabaseState>
{
    [Initialize]
    public DatabaseState Init() => new DatabaseState(new InMemoryDb());
    
    [Rule]
    public void InsertItem(
        DatabaseState state,
        [From<AsciiStrings>] string key,
        [From<SmallInts>] int value)
    {
        state.Db.Insert(key, value);
        state.Model[key] = value;  // update model
    }
    
    [Rule]
    [Precondition(nameof(HasItems))]
    public void DeleteItem(DatabaseState state, [Consumes] string key)
    {
        state.Db.Delete(key);
        state.Model.Remove(key);
    }
    
    [Invariant]
    public void ModelMatchesDb(DatabaseState state)
    {
        foreach (var (k, v) in state.Model)
            Assert.Equal(v, state.Db.Get(k));
    }
    
    bool HasItems(DatabaseState state) => state.Model.Count > 0;
}
```

This requires:
- A step scheduler (choose which rule to execute each step)
- Bundle management (named collections of generated values that rules can consume)
- Precondition checking
- Invariant verification after each step
- Shrinking of the *step sequence* (not just individual values)

#### 3.2 Targeted property-based testing

Hypothesis's `target()` function for optimization-guided exploration:

```csharp
[Property]
public void FindPerformanceRegression(List<int> data)
{
    var elapsed = Benchmark(() => MySort(data));
    Hypothesis.Target(elapsed.TotalMilliseconds, label: "sort_time");
    Assert.True(elapsed < TimeSpan.FromSeconds(1));
}
```

Implementation: use hill-climbing / gradient-free optimization in the Conjecture engine to steer generation toward maximizing the target metric.

#### 3.3 Recursive / tree-shaped strategies

```csharp
// Recursive data structures
var jsonStrategy = Strategies.Deferred(() =>
    Strategies.OneOf(
        Strategies.Just(JsonValue.Null),
        Strategies.Integers().Select(i => JsonValue.Number(i)),
        Strategies.Strings().Select(s => JsonValue.String(s)),
        Strategies.Lists(jsonStrategy).Select(l => JsonValue.Array(l)),
        Strategies.Dictionaries(Strategies.Strings(), jsonStrategy)
            .Select(d => JsonValue.Object(d))
    ));
```

Requires:
- Lazy/deferred strategy construction
- Depth/size budgeting to prevent infinite recursion
- Good shrinking of tree structures

#### 3.4 Advanced source generation

Build on the Phase 1 `[Arbitrary]` source generator with more sophisticated capabilities:

- **Recursive type support** — detect cycles in the type graph (e.g., `TreeNode` with `List<TreeNode> Children`) and generate depth-bounded strategies automatically using `Strategies.Deferred()`
- **Inheritance hierarchies** — `[Arbitrary]` on an abstract base or interface generates a `OneOf(...)` strategy across all known concrete implementations in the assembly
- **Generic type support** — `[Arbitrary] partial record Pair<T>(T Left, T Right)` generates an open generic strategy `PairStrategy<T>` that composes with any `Strategy<T>`
- **Struct strategies** — extend the generator to handle `record struct` types, ensuring zero heap allocation for generated values
- **Analyzer companion** — ship diagnostics beyond `CON001`:
  - `CON002`: Type is not `partial` but has `[Arbitrary]` — error with fix suggestion
  - `CON003`: Circular dependency detected without `[Arbitrary]` on dependent type — warning
  - `CON004`: Constructor parameter has no registered strategy and no `[StrategyOverride]` — error
  - `CON005`: `[StrategyOverride<T>]` type `T` does not produce a compatible output type — error

#### 3.5 Additional test framework adapters
- **NUnit:** `[Property]` attribute via `ITestBuilder`
- **MSTest:** `[PropertyTestMethod]` via `ITestDataSource`
- Each in a separate NuGet package

#### 3.6 F# idiomatic API (`Hypothesis.FSharp`)

`Hypothesis.Core` is a standard .NET class library — F# can consume it directly. But the C# API surface (attributes, generic constraints, fluent builders) feels unnatural in F#. An idiomatic F# wrapper makes the library feel native to both communities without duplicating any engine logic.

**Design principle:** `Hypothesis.Core` stays unchanged. The F# packages are thin wrappers that map F# conventions onto the existing engine. The `gen { }` computation expression internally calls `strategy.Draw(data)` on the same `ConjectureData` byte stream, so shrinking, the database, and all engine behaviour are identical.

**Project structure:**

```
Hypothesis.FSharp           — Gen module, computation expression, auto-derivation
Hypothesis.FSharp.Xunit     — F#-friendly [Property] discovery (optional)
Hypothesis.FSharp.Expecto   — Expecto integration (preferred by many F# devs)
```

**`Gen` module — pipe-friendly strategy functions:**

```fsharp
/// Module providing F#-idiomatic strategy constructors and combinators.
/// All functions return Strategy<'T> and compose via |> piping.
[<RequireQualifiedAccess>]
module Gen =

    /// Generates integers in [min, max].
    val int: min:int -> max:int -> Strategy<int>

    /// Generates integers across the full int range.
    val int': Strategy<int>

    /// Generates strings with default settings.
    val string: Strategy<string>

    /// Generates strings with length bounds.
    val stringOf: minLength:int -> maxLength:int -> Strategy<string>

    /// Generates lists of values from the given strategy.
    val listOf: Strategy<'a> -> Strategy<'a list>

    /// Generates lists with size bounds.
    val listOfN: min:int -> max:int -> Strategy<'a> -> Strategy<'a list>

    /// Chooses uniformly from the given values.
    val oneOf: 'a list -> Strategy<'a>

    /// Chooses from the given strategies.
    val choiceOf: Strategy<'a> list -> Strategy<'a>

    /// Filters generated values.
    val filter: ('a -> bool) -> Strategy<'a> -> Strategy<'a>

    /// Transforms generated values.
    val map: ('a -> 'b) -> Strategy<'a> -> Strategy<'b>

    /// Monadic bind for dependent generation.
    val bind: ('a -> Strategy<'b>) -> Strategy<'a> -> Strategy<'b>

    /// Always produces the given value.
    val constant: 'a -> Strategy<'a>

    /// Generates option values — None or Some of the given strategy.
    val optional: Strategy<'a> -> Strategy<'a option>

    /// Generates booleans.
    val bool: Strategy<bool>

    /// Generates floats in [min, max].
    val float: min:float -> max:float -> Strategy<float>

    /// Imperative composition (wraps Strategies.Compose).
    val compose: (IGeneratorContext -> 'a) -> Strategy<'a>
```

Usage:

```fsharp
// Pipe-friendly composition
let positiveEvenInts =
    Gen.int 1 10000
    |> Gen.filter (fun n -> n % 2 = 0)

let sortedPairs =
    Gen.int' |> Gen.bind (fun x ->
        Gen.int x System.Int32.MaxValue |> Gen.map (fun y -> x, y))
```

**`gen { }` computation expression (Python `@composite` equivalent):**

```fsharp
/// Computation expression builder for imperative strategy composition.
/// Each let! draws a value from a strategy; the drawn value can be used
/// to configure subsequent draws.
type GenBuilder() =
    member _.Bind(strategy, f) = strategy |> Gen.bind f
    member _.Return(x) = Gen.constant x
    member _.ReturnFrom(strategy) = strategy
    member _.Zero() = Gen.constant ()
    member _.Delay(f) = Gen.bind f (Gen.constant ())

let gen = GenBuilder()
```

Usage:

```fsharp
// Clean, readable, fully shrinkable
let httpRequest =
    gen {
        let! method = Gen.oneOf [ "GET"; "POST"; "PUT"; "DELETE" ]
        let! path = Gen.string |> Gen.map (fun s -> "/" + s)
        let! body =
            if method <> "GET" then
                Gen.listOf (Gen.int 0 255)
                |> Gen.map (fun bytes -> Some (Array.ofList bytes))
            else
                Gen.constant None
        let! headerCount = Gen.int 0 5
        let! headers =
            Gen.listOfN headerCount headerCount (
                gen {
                    let! key = Gen.stringOf 1 20
                    let! value = Gen.string
                    return key, value
                }
            )
        return {
            Method = method
            Path = path
            Body = body
            Headers = Map.ofList headers
        }
    }

// Nested gen { } blocks compose naturally
let validOrder =
    gen {
        let! customerId = Gen.compose (fun g -> g.Generate(Strategies.Guids()))
        let! itemCount = Gen.int 1 10
        let! items =
            Gen.listOfN itemCount itemCount (
                gen {
                    let! sku = Gen.stringOf 3 8
                    let! qty = Gen.int 1 100
                    let! price = Gen.float 0.01 999.99
                    return { Sku = sku; Quantity = qty; UnitPrice = price }
                }
            )
        let! discount = Gen.float 0.0 0.3
        return { CustomerId = customerId; Items = items; Discount = discount }
    }
```

**F#-native test API (no attributes):**

```fsharp
open Hypothesis.FSharp

// Standalone property check — returns a result, doesn't throw
let result =
    Property.check "reverse reverse is identity"
        (Gen.listOf Gen.int')
        (fun xs -> xs |> List.rev |> List.rev = xs)

// With settings
let result =
    Property.checkWith
        { ConjectureSettings.Default with MaxExamples = 500 }
        "sort is idempotent"
        (Gen.listOf Gen.int')
        (fun xs -> List.sort (List.sort xs) = List.sort xs)

// Multi-parameter properties via tuples
let result =
    Property.check2 "addition is commutative"
        (Gen.int -1000 1000) (Gen.int -1000 1000)
        (fun a b -> a + b = b + a)
```

**Expecto integration:**

```fsharp
open Expecto
open Hypothesis.FSharp.Expecto

let tests =
    testList "property tests" [
        testProperty "reverse reverse is identity"
            (Gen.listOf Gen.int')
            (fun xs ->
                Expect.equal
                    (xs |> List.rev |> List.rev)
                    xs
                    "double reverse should be identity")

        testProperty "sort is stable"
            (Gen.listOf (Gen.int 0 10))
            (fun xs ->
                let sorted = List.sort xs
                Expect.isTrue
                    (List.pairwise sorted |> List.forall (fun (a, b) -> a <= b))
                    "sorted list should be ordered")
    ]

[<EntryPoint>]
let main args = runTestsWithCLIArgs [] args tests
```

**Auto-derivation for F# types:**

C# source generators don't target F#, so `[Arbitrary]` won't work. Instead, use `FSharp.Reflection` with compiled expression caching:

```fsharp
/// Automatically derives a strategy for F# record types and
/// discriminated unions using FSharp.Reflection.
/// The reflection cost is paid once; subsequent draws use a
/// cached compiled delegate.
module Gen =

    /// Derives a strategy for the given F# type.
    /// Supports records, discriminated unions, and tuple types.
    val auto<'T> : Strategy<'T>
```

Implementation:

```fsharp
// F# records — inspect fields, compose strategies
type Person = { Name: string; Age: int; Email: string option }
let personGen = Gen.auto<Person>
// Internally: gen { let! name = Gen.string
//                   let! age = Gen.int'
//                   let! email = Gen.optional Gen.string
//                   return { Name = name; Age = age; Email = email } }

// Discriminated unions — OneOf across all cases
type Shape =
    | Circle of radius: float
    | Rectangle of width: float * height: float
    | Triangle of a: float * b: float * c: float

let shapeGen = Gen.auto<Shape>
// Internally: Gen.choiceOf [
//     gen { let! r = Gen.float' in return Circle r }
//     gen { let! w = Gen.float' in let! h = Gen.float' in return Rectangle(w, h) }
//     gen { let! a = Gen.float' in let! b = Gen.float' in let! c = Gen.float' in return Triangle(a, b, c) }
// ]

// Nested types work recursively
type Tree<'T> = Leaf of 'T | Branch of Tree<'T> * Tree<'T>
let treeGen = Gen.auto<Tree<int>>  // depth-bounded automatically
```

This uses `FSharp.Reflection.FSharpType.GetRecordFields`, `FSharpType.GetUnionCases`, and `FSharpValue.MakeRecord` / `FSharpValue.MakeUnion`. The reflection is performed once per type; a compiled `Func<IGeneratorContext, 'T>` is cached and reused for all subsequent draws. This is the one area where the "no reflection" principle requires a pragmatic exception — F# doesn't have source generators, and type providers would be overkill for this use case.

**Trimming note:** `Gen.auto<'T>` uses runtime reflection and is not trim-safe. Mark it with `[<RequiresUnreferencedCode>]`. For trimmed/AOT F# scenarios, users should construct strategies manually via `gen { }` or `Gen.compose`.

**What stays shared vs. what's F#-specific:**

| Component | Shared (`Hypothesis.Core`) | F#-specific (`Hypothesis.FSharp`) |
|---|---|---|
| Conjecture engine | ✓ | |
| Shrinker | ✓ | |
| Byte stream / `ConjectureData` | ✓ | |
| Example database | ✓ | |
| Settings | ✓ | |
| Built-in strategies | ✓ | `Gen` module wraps them |
| `Strategy<T>` base class | ✓ | |
| `IGeneratorContext` | ✓ | `gen { }` CE uses it internally |
| `[Property]` attribute | ✓ (C# style) | `Property.check` function-based API |
| `[Arbitrary]` source gen | ✓ (C# only) | `Gen.auto<'T>` reflection-based |
| Formatters | ✓ | F# records/DUs have good default `ToString()` |
| Expecto integration | | ✓ |
| Computation expression | | ✓ |

---

### Phase 4: Polish, Performance & Ecosystem (Ongoing)

#### 4.1 Performance
- Benchmark against FsCheck, CsCheck, fast-check (JS) on standard property test suites
- Profile the shrinker — this is typically the bottleneck
- `Span<byte>` / `Memory<byte>` / `ArrayPool` are baked in from Phase 0 (see Section 3.3) — Phase 4 focuses on profiling and optimizing the higher-level patterns
- Investigate `stackalloc` for small fixed-size temporaries in hot shrinker loops
- Pool `ConjectureData` instances if profiling shows `Dispose`/re-rent is a bottleneck
- Consider `FrozenDictionary` / `FrozenSet` for read-only lookup tables (strategy registry, settings)

#### 4.2 Documentation
- Comprehensive README with quick-start
- API reference docs (auto-generated via DocFX or similar)
- Porting guide: "Hypothesis concepts → Conjecture equivalents"
- Tutorial series: property-based testing for C# developers
- Contributing guide

#### 4.3 Ecosystem integrations
- **Faker.NET bridge** — use Faker generators as strategies
- **AutoFixture bridge** — interop with AutoFixture's specimen builders
- **Rider / VS plugin** — inline counterexample display
- **Stryker.NET integration** — use property tests for mutation testing

#### 4.4 Advanced optimizations (profile-guided, post-1.0)

These are speculative optimizations that should only be pursued after profiling real-world usage shows they're warranted. Each carries complexity cost — don't implement without benchmark evidence.

**1. SIMD-accelerated buffer operations**

The shrinker frequently compares buffers (lexicographic ordering) and checks for all-zero blocks. While `MemoryExtensions.SequenceCompareTo` already benefits from .NET 10's auto-vectorization, hot-spot-specific SIMD paths can short-circuit earlier:

```csharp
/// <summary>
/// Fast all-zeros check using hardware SIMD intrinsics.
/// Falls back to scalar loop on unsupported hardware.
/// </summary>
internal static bool IsAllZeros(ReadOnlySpan<byte> span)
{
    if (Vector256.IsHardwareAccelerated && span.Length >= Vector256<byte>.Count)
    {
        int i = 0;
        for (; i + Vector256<byte>.Count <= span.Length; i += Vector256<byte>.Count)
        {
            var vec = Vector256.Create(span.Slice(i, Vector256<byte>.Count));
            if (vec != Vector256<byte>.Zero)
                return false;
        }
        // check remaining bytes
        for (; i < span.Length; i++)
            if (span[i] != 0) return false;
        return true;
    }

    // scalar fallback
    return span.IndexOfAnyExcept((byte)0) < 0;
}
```

This matters for the zero-block pass, which tests every block in the buffer. A 256-bit check processes 32 bytes per comparison vs. 1 byte scalar. Profile first — the BCL's `IndexOfAnyExcept` may already be fast enough on .NET 10.

**2. Frozen block list after generation**

Once a `ConjectureData` run completes, its block list never changes. Converting from `List<Block>` to a plain array at that point improves shrinker iteration:

```csharp
/// <summary>
/// Freezes the mutable block list into a fixed array after generation completes.
/// Eliminates bounds checking overhead and improves cache locality for the
/// shrinker's repeated iteration over blocks.
/// </summary>
public ConjectureResult Freeze()
{
    var frozenBlocks = CollectionsMarshal.AsSpan(blocks).ToArray();
    return new ConjectureResult(
        rentedBuffer[..length].ToArray(),  // materialize exact-length copy
        frozenBlocks,
        Status);
}
```

The shrinker then works with `ReadOnlySpan<Block>` over this array. With `Block` as a `readonly record struct` of ~16 bytes, an array of 50 blocks fits in a single cache line sequence — much better locality than a `List<T>` with its internal array, count field, and version tracking.

**3. Tiered shrink pass scheduling**

Formalize a cost model for shrink passes so cheap passes run first and expensive passes only run when cheap ones stop making progress:

```csharp
/// <summary>
/// Shrink passes ordered by cost-per-improvement ratio.
/// Cheap passes that frequently succeed run first.
/// </summary>
internal enum ShrinkPassPriority
{
    /// <summary>Nearly free — zero a single block.</summary>
    ZeroBlocks = 0,

    /// <summary>Cheap — binary search within a block's range.</summary>
    MinimizeIntegers = 1,

    /// <summary>Moderate — try deleting each block.</summary>
    DeleteBlocks = 2,

    /// <summary>Moderate — lexicographic minimize per block.</summary>
    LexicographicMinimize = 3,

    /// <summary>Expensive — swap or redistribute between block pairs.</summary>
    SwapAndRedistribute = 4,

    /// <summary>Most expensive — adaptive passes that learn from history.</summary>
    Adaptive = 5,
}
```

Run all Priority 0 passes to fixpoint, then Priority 1, etc. If a higher-priority pass makes progress, restart from Priority 0. This converges faster because cheap passes often eliminate work that expensive passes would otherwise attempt.

**4. Parallel test generation with `IThreadPoolWorkItem`**

The shrink loop is sequential, but the *generation* phase (finding the first failure) can run multiple buffers concurrently. Using `IThreadPoolWorkItem` avoids `Task` allocation overhead:

```csharp
/// <summary>
/// Generates and tests multiple buffers concurrently during the
/// exploration phase. First failure wins; remaining attempts are cancelled.
/// </summary>
internal sealed class ParallelExplorer<T> : IThreadPoolWorkItem
{
    private readonly Strategy<T> strategy;
    private readonly Action<T> test;
    private readonly CancellationTokenSource cts;
    private ConjectureData? interestingResult;

    public void Execute()
    {
        using var data = ConjectureData.Create(bufferSize);
        FillBuffer(data, Random.Shared);

        if (cts.IsCancellationRequested) return;

        T value = strategy.Draw(data);
        try
        {
            test(value);
        }
        catch
        {
            // First failure wins — cancel other workers
            Interlocked.CompareExchange(ref interestingResult, data, null);
            cts.Cancel();
        }
    }
}
```

This is only beneficial when individual test executions are expensive (>1ms each). For sub-microsecond tests, the synchronization overhead dominates. Gate behind `ConjectureSettings.ParallelGeneration = true` (off by default).

**5. Internal UTF-8 string representation**

Strings are the most expensive type to generate and shrink because each shrink attempt involves `byte[]` → `string` conversion (UTF-16 encoding, heap allocation). Keeping strings as UTF-8 `byte[]` internally and only converting to `string` at the test boundary reduces allocation pressure:

```csharp
/// <summary>
/// Generates strings as UTF-8 byte sequences internally.
/// Converts to System.String only when the value is passed to the test function.
/// During shrinking, all operations stay in UTF-8 — no intermediate string allocations.
/// </summary>
internal sealed class Utf8StringStrategy : Strategy<string>
{
    internal override string Draw(ConjectureData data)
    {
        // Draw raw UTF-8 bytes
        int length = (int)data.DrawInteger(0, (ulong)maxLength);
        ReadOnlySpan<byte> utf8 = data.DrawBytes(length);

        // Validate and fix UTF-8 (ensure no partial sequences)
        // Convert to string only here — the shrinker works on the raw bytes
        return Encoding.UTF8.GetString(utf8);
    }
}
```

The shrinker operates on the underlying byte buffer, so string shrinking becomes byte shrinking — no `string` objects created during the shrink loop at all. The `string` is only materialized when the test function is called. This can eliminate thousands of intermediate string allocations per shrink cycle.

**6. `ConjectureData` as `ref struct`**

Currently `ConjectureData` is a `class` (heap-allocated) with `IDisposable` for pool return. Each generation cycle allocates a `ConjectureData` object on the heap. With thousands of cycles per test, this adds GC pressure.

Making `ConjectureData` a `ref struct` would move it to the stack entirely:

```csharp
internal ref struct ConjectureData
{
    private byte[] rentedBuffer;
    private int length;
    private int index;
    // List<Block> can't be a field in a ref struct — use a rented Block[] instead
    private Block[] blockBuffer;
    private int blockCount;

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(rentedBuffer);
        ArrayPool<Block>.Shared.Return(blockBuffer);
    }
}
```

Tradeoffs:
- Can't implement `IDisposable` interface (but can have a `Dispose()` method, and `using` works with the duck-typed pattern)
- Can't store in fields, pass to async methods, or use in lambdas — the engine loop must be restructured to keep it stack-scoped
- `List<Block>` must become a rented `Block[]` with manual count tracking (since `ref struct` can't hold reference-type fields in all scenarios)

This is a significant refactor with modest benefit (one fewer heap allocation per cycle). Only pursue if benchmarks show `ConjectureData` allocation is a measurable fraction of total overhead.

**Decision framework: when to pursue these**

| Optimization | Implement if... | Expected impact |
|---|---|---|
| SIMD buffer operations | Zero-block pass is >5% of shrinker time in profiles | 2–4x faster for that specific pass |
| Frozen block list | Block iteration shows cache misses in profiler | 10–20% faster shrinker iteration |
| Tiered shrink scheduling | Shrinker converges slowly on real-world tests | 30–50% fewer total shrink attempts |
| Parallel generation | Users report slow exploration for expensive tests | Near-linear speedup with core count |
| UTF-8 string internals | String-heavy properties dominate shrink time | 5–10x fewer allocations for string tests |
| `ref struct` ConjectureData | `ConjectureData` allocation visible in allocation profiler | Eliminates ~1 alloc per generation cycle |

---

## 5. Technical Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Conjecture engine is complex and subtle | High — bugs here affect all tests | Port tests from Hypothesis's own test suite; property-test the engine itself |
| Shrinker performance in C# may differ from Python | Medium — C# is faster per-op but the algorithm matters | Benchmark early, profile, optimize hot paths with Span/stackalloc |
| xUnit's extensibility model may not support all features | Medium | Prototype the integration in Phase 0; fall back to test-runner-agnostic core |
| .NET generics don't have the same flexibility as Python's duck typing | Medium | Lean on `Strategy<T>` as the universal abstraction; use source generators for ergonomics |
| Maintaining parity with upstream Hypothesis | Low-medium | Track Hypothesis releases; document intentional divergences |
| Open-source adoption | Low | Good docs, NuGet packaging, blog posts, conference talks |

---

## 6. Project Concerns & Open Questions

Beyond the technical architecture, several project-level decisions will shape whether Conjecture succeeds as an open-source library.

### 6.1 Naming, Branding & Upstream Relations

Check NuGet for naming conflicts before committing to `Conjecture` as the package name. More importantly, reach out to David MacIver (the original Hypothesis author) early. The MPL-2.0 license permits derivative works, but a heads-up is good etiquette and could lead to listing the port as an official community project on [hypothesis.works](https://hypothesis.works). Being endorsed by the upstream project is a significant adoption signal.

If the Hypothesis team prefers you don't use the name directly, alternatives like `Conjecture.NET` (referencing the engine) or `PropertyCheck` would still communicate the purpose clearly.

### 6.2 Versioning & API Stability

Ship `0.x` versions throughout Phases 0–2 with explicit "API may change" messaging. This gives early adopters access while preserving freedom to iterate on the public surface. Key milestones:

- `0.1.0` — Phase 0 complete: engine works, basic strategies, minimal xUnit integration
- `0.5.0` — Phase 1 complete: full strategy library, combinators, source generator
- `0.9.0` — Phase 2 complete: production shrinker, database, polished xUnit integration
- `1.0.0` — API frozen, SemVer commitment begins

From `1.0.0` onward, follow strict SemVer: breaking changes only in major versions, new features in minor versions, bug fixes in patches. Document the public API surface explicitly (which types/methods are public vs. internal) so contributors know what constitutes a breaking change.

### 6.3 Community & Governance

For a library targeting broad .NET adoption, establish these from day one:

- **`CONTRIBUTING.md`** — clear expectations for PRs: must include tests (ideally self-tests where applicable), must pass CI, must include XML docs on public members. State whether you accept unsolicited feature PRs or prefer issues-first discussion.
- **Issue templates** — separate templates for bug reports (with repro steps + minimal failing property), feature requests, and API design discussions.
- **`CODE_OF_CONDUCT.md`** — standard Contributor Covenant. Non-negotiable for a welcoming open-source project.
- **GitHub Discussions** — enable for API design conversations. Major design decisions (e.g., "should `Strategy<T>` be an abstract class or an interface?") should be discussed in the open before implementation.
- **Decision records** — maintain an `docs/decisions/` directory with lightweight Architecture Decision Records (ADRs) for significant design choices and intentional divergences from Python Hypothesis. These are invaluable for onboarding new contributors.
- **Governance model** — for a solo-founded project, a benevolent dictator model is fine initially. Document it explicitly: "the maintainer has final say on design decisions, but all significant changes are discussed publicly first."

### 6.4 Porting Hypothesis's Bug History as Regression Tests

The Python Hypothesis repo has years of bug reports with minimal reproductions, particularly around the shrinker and edge cases in strategies (float handling, Unicode, empty collections, etc.). Port a curated subset of these as regression tests for the .NET engine:

- Browse the `hypothesis-python` issue tracker for issues tagged `bug` with clear reproductions
- Focus on shrinker bugs (these are the hardest to get right and the most impactful on user experience)
- Translate the Python reproduction into a C# `[Property]` test that would have caught the bug
- Maintain these in a dedicated `Conjecture.SelfTests/Regressions/` directory with issue references in comments

This gives you a concrete correctness benchmark against the mature Python implementation and catches cases your own self-tests might miss.

### 6.5 Roslyn Analyzers for Property Test Quality

Beyond the `[Arbitrary]` source generator, ship a companion Roslyn analyzer package (`Hypothesis.Analyzers`) that warns on common property test anti-patterns:

| Diagnostic ID | Severity | Description |
|---|---|---|
| `CON100` | Warning | `[Property]` method has no assertions — test will always pass |
| `CON101` | Warning | `.Where()` predicate is statically determinable as rejecting >50% of values |
| `CON102` | Info | `[Property]` method is synchronous but could be async (database/IO in body) |
| `CON103` | Warning | Strategy composition creates an unreachable type (e.g., `Integers(10, 5)` where min > max) |
| `CON104` | Warning | `Assume.That()` called unconditionally with a literal `false` — test will always be skipped |
| `CON105` | Info | Consider using `[From<T>]` instead of default strategy for more targeted generation |

Ship these as a separate NuGet package so users can opt in. Include code fix providers where possible (e.g., `CON103` → swap min and max).

### 6.6 Deterministic Replay & CI Strategy

The example database stores failing buffers for replay, but CI environments have ephemeral filesystems. Address this explicitly:

**Option A: Committed database (Python Hypothesis approach)**

Python Hypothesis stores its database in a `.hypothesis/` directory that can be committed to the repo. The .NET port can follow suit:

```
my-project/
├── .hypothesis/            # committed to git
│   └── examples/
│       └── MyTests.SortIsIdempotent  # binary file with failing buffer
├── src/
└── tests/
```

Pros: failing examples survive across CI runs and developer machines. Cons: binary files in git, potential merge conflicts.

**Option B: Seed-based reproduction only**

On failure, print the seed in the test output:

```
Hypothesis found a failing example:
  Counterexample: [3, -1, 0]
  Seed: 0xDEADBEEF42
  
  To reproduce, add: [Property(Seed = 0xDEADBEEF42)]
```

Pros: no files to manage. Cons: seed reproduction is fragile across library versions (buffer layout may change).

**Recommendation:** Support both. Default to the committed database approach (matching Python Hypothesis), with seed-based reproduction as a fallback. Document the tradeoffs and let users choose via `ConjectureSettings.Database`.

### 6.7 Performance Regression Testing in CI

A library that claims high performance should detect regressions automatically. Integrate BenchmarkDotNet results into CI:

```yaml
# GitHub Actions workflow
- name: Run benchmarks
  run: dotnet run --project Hypothesis.Benchmarks -c Release -- --export json

- name: Check for regressions
  uses: benchmark-action/github-action-benchmark@v1
  with:
    tool: benchmarkdotnet
    output-file-path: BenchmarkDotNet.Artifacts/results.json
    alert-threshold: '115%'     # fail if >15% slower
    fail-on-alert: true
    comment-on-alert: true      # post PR comment on regression
```

Key benchmarks to track:

- **Draw throughput** — draws per second for `Integers()`, `Strings()`, `Lists(Integers())`
- **Shrink iterations** — number of shrink attempts to reach minimal counterexample for standard failures
- **Shrink wall time** — time to shrink a standard failing property to minimal form
- **Memory per draw** — allocation bytes per draw operation (target: zero in steady state)
- **End-to-end property** — total time for 100 examples of a representative property test

Store historical benchmark results in the repo (or via GitHub Pages) so contributors can see trends over time.

### 6.8 Licensing Nuance

The plan says MPL-2.0 to match Hypothesis. Worth noting: MPL-2.0 is file-level copyleft, meaning any file you copy or derive directly from Hypothesis source must stay MPL-2.0, but new files you write from scratch can be under a more permissive license if desired. For simplicity, applying MPL-2.0 to the entire project is cleanest. However, if you want maximum adoption in corporate environments where legal teams are cautious about copyleft, you could dual-license under MPL-2.0 and MIT — consult with the Hypothesis team on their preference.

---

## 7. Reference Implementation Map

Key files in the Hypothesis Python codebase and their C# counterparts:

| Python module | Purpose | C# target |
|---|---|---|
| `hypothesis/core.py` | Test runner loop, `given()` | `Hypothesis.Core/Engine/TestRunner.cs` |
| `hypothesis/internal/conjecture/engine.py` | Conjecture engine | `Hypothesis.Core/Engine/ConjectureEngine.cs` |
| `hypothesis/internal/conjecture/data.py` | `ConjectureData`, buffer management | `Hypothesis.Core/Engine/ConjectureData.cs` |
| `hypothesis/internal/conjecture/shrinker.py` | Shrinking passes | `Hypothesis.Core/Shrinking/Shrinker.cs` |
| `hypothesis/strategies/` | All built-in strategies | `Hypothesis.Core/Strategies/` |
| `hypothesis/stateful.py` | Stateful testing | `Hypothesis.Core/Stateful/StateMachine.cs` |
| `hypothesis/database.py` | Example database | `Hypothesis.Core/Database/` |
| `hypothesis/settings.py` | Settings and profiles | `Hypothesis.Core/Settings/ConjectureSettings.cs` |
| `hypothesis/reporting.py` | Output formatting | `Hypothesis.Core/Reporting/` |

---

## 8. Timeline Summary

| Phase | Duration | Milestone |
|---|---|---|
| **Phase 0:** Foundation & PoC | 4–6 weeks | End-to-end property test with shrinking |
| **Phase 1:** Core strategies | 6–8 weeks | Comprehensive type coverage, LINQ combinators |
| **Phase 2:** Shrinker & infra | 6–8 weeks | Production-quality shrinker, database, xUnit polish |
| **Phase 3:** Advanced features | 8–12 weeks | Stateful testing, targeted PBT, source generators |
| **Phase 4:** Polish & ecosystem | Ongoing | Docs, perf, adapters, community |

**Total to MVP (Phases 0–2): ~16–22 weeks**
**Total to feature-complete (Phases 0–3): ~24–34 weeks**

---

## 9. Getting Started Checklist

- [ ] Check NuGet for naming conflicts; reach out to David MacIver / HypothesisWorks about the port
- [ ] Create GitHub repo with MPL-2.0 license, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue templates
- [ ] Set up solution structure with projects listed in Phase 0.1 (including `Conjecture.SelfTests`)
- [ ] Configure `.editorconfig` with naming conventions (no underscore prefix, XML doc enforcement)
- [ ] Read Hypothesis's `data.py` and `engine.py` thoroughly — these are the ground truth
- [ ] Implement `ConjectureData` with a pooled byte buffer and Span-based draw primitives
- [ ] Write the first property test: `Integers().Where(n => n > 0)` shrinks to `1`
- [ ] Write the first self-test: `IntegersRespectBounds` (Section 0.7)
- [ ] Set up CI with build, test, NuGet pack, and benchmark baseline
- [ ] Publish `0.1.0-alpha` to NuGet once Phase 0 exit criteria are met
- [ ] Open GitHub Discussions for API design feedback before Phase 1
- [ ] Begin curating Hypothesis Python bug reports for regression test porting (Section 6.4)

---

*This plan is based on Hypothesis v6.x architecture. The Python source at [github.com/HypothesisWorks/hypothesis](https://github.com/HypothesisWorks/hypothesis) is the canonical reference.*
