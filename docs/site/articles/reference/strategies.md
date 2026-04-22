# Strategies reference

All factory methods on the `Generate` static class, plus LINQ combinators from `StrategyExtensions`.

## Numeric strategies

### `Generate.Integers<T>()`

```csharp
Strategy<T> Generate.Integers<T>()
    where T : IBinaryInteger<T>
```

Generates values across the full range of `T`. Works with `int`, `long`, `byte`, `short`, `uint`, `ulong`, `sbyte`, `ushort`, `nint`, `nuint`.

### `Generate.Integers<T>(T min, T max)`

```csharp
Strategy<T> Generate.Integers<T>(T min, T max)
    where T : IBinaryInteger<T>
```

Generates values in `[min, max]` inclusive. `min` must be ≤ `max`.

### `Generate.Doubles()`

```csharp
Strategy<double> Generate.Doubles()
```

Generates any `double`, including `NaN`, `+Infinity`, `-Infinity`, and denormals.

### `Generate.Doubles(double min, double max)`

```csharp
Strategy<double> Generate.Doubles(double min, double max)
```

Generates `double` values in `[min, max]`. Neither bound may be `NaN`.

### `Generate.Floats()`

```csharp
Strategy<float> Generate.Floats()
```

Generates any `float`, including `NaN`, `+Infinity`, `-Infinity`, and denormals.

### `Generate.Floats(float min, float max)`

```csharp
Strategy<float> Generate.Floats(float min, float max)
```

Generates `float` values in `[min, max]`. Neither bound may be `NaN`.

### `Generate.Booleans()`

```csharp
Strategy<bool> Generate.Booleans()
```

Generates `true` and `false` with equal probability.

### `Generate.Bytes(int size)`

```csharp
Strategy<byte[]> Generate.Bytes(int size)
```

Generates a `byte[]` of exactly `size` bytes. `size` must be ≥ 0.

## String strategies

See [String strategies reference](string-strategies.md) for `Strings`, `Text`, `Identifiers`, `NumericStrings`, and `VersionStrings`.

## Date and time strategies

### `Generate.DateTimeOffsets()`

```csharp
Strategy<DateTimeOffset> Generate.DateTimeOffsets()
```

Generates `DateTimeOffset` values across the full range.

### `Generate.DateTimeOffsets(DateTimeOffset min, DateTimeOffset max)`

```csharp
Strategy<DateTimeOffset> Generate.DateTimeOffsets(DateTimeOffset min, DateTimeOffset max)
```

Generates `DateTimeOffset` values in `[min, max]`.

### `Generate.TimeSpans()`

```csharp
Strategy<TimeSpan> Generate.TimeSpans()
```

Generates `TimeSpan` values across the full range.

### `Generate.TimeSpans(TimeSpan min, TimeSpan max)`

```csharp
Strategy<TimeSpan> Generate.TimeSpans(TimeSpan min, TimeSpan max)
```

Generates `TimeSpan` values in `[min, max]`.

### `Generate.DateOnlyValues()`

```csharp
Strategy<DateOnly> Generate.DateOnlyValues()
```

Generates `DateOnly` values across the full range.

### `Generate.DateOnlyValues(DateOnly min, DateOnly max)`

```csharp
Strategy<DateOnly> Generate.DateOnlyValues(DateOnly min, DateOnly max)
```

Generates `DateOnly` values in `[min, max]`.

### `Generate.TimeOnlyValues()`

```csharp
Strategy<TimeOnly> Generate.TimeOnlyValues()
```

Generates `TimeOnly` values across the full range.

### `Generate.TimeOnlyValues(TimeOnly min, TimeOnly max)`

```csharp
Strategy<TimeOnly> Generate.TimeOnlyValues(TimeOnly min, TimeOnly max)
```

Generates `TimeOnly` values in `[min, max]`.

For boundary-focused extensions (`.NearMidnight()`, `.NearDstTransition()`, etc.) and `Generate.TimeZones`/`Generate.ClockSet` factory methods, see [Time strategies reference](time-strategies.md).

## Byte buffer strategies

### `Generate.FromBytes<T>(ReadOnlySpan<byte> buffer)`

```csharp
Strategy<T> Generate.FromBytes<T>(ReadOnlySpan<byte> buffer)
```

Replays a value of type `T` from a fixed byte buffer using the default strategy for `T`. The buffer is the same format stored by the example database. Useful for deterministic replay and round-trip testing.

## Collection strategies

### `Generate.Lists<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<List<T>> Generate.Lists<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
```

Generates `List<T>` with length in `[minSize, maxSize]`. `minSize` must be ≥ 0 and ≤ `maxSize`.

### `Generate.Sets<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<IReadOnlySet<T>> Generate.Sets<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
```

Generates `IReadOnlySet<T>` (backed by `HashSet<T>`) with cardinality in `[minSize, maxSize]`.

### `Generate.Dictionaries<TKey, TValue>(Strategy<TKey> keys, Strategy<TValue> values, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<IReadOnlyDictionary<TKey, TValue>> Generate.Dictionaries<TKey, TValue>(
    Strategy<TKey> keys,
    Strategy<TValue> values,
    int minSize = 0,
    int maxSize = 100)
```

Generates `IReadOnlyDictionary<TKey, TValue>` with entry count in `[minSize, maxSize]`.

## Choice strategies

### `Generate.Just<T>(T value)`

```csharp
Strategy<T> Generate.Just<T>(T value)
```

Always produces the same `value`. Useful as a degenerate case in `OneOf` or `Recursive`.

### `Generate.OneOf<T>(params Strategy<T>[] strategies)`

```csharp
Strategy<T> Generate.OneOf<T>(params Strategy<T>[] strategies)
```

Picks one strategy per example uniformly at random, then generates a value from it.

### `Generate.SampledFrom<T>(IReadOnlyList<T> values)`

```csharp
Strategy<T> Generate.SampledFrom<T>(IReadOnlyList<T> values)
```

Picks one element from `values` uniformly at random. `values` must be non-empty.

### `Generate.Enums<T>()`

```csharp
Strategy<T> Generate.Enums<T>()
    where T : struct, Enum
```

Picks one declared value from `T` uniformly at random.

### `Generate.Nullable<T>(Strategy<T> inner)`

```csharp
Strategy<T?> Generate.Nullable<T>(Strategy<T> inner)
    where T : struct
```

Returns `null` approximately 10% of the time; otherwise generates a value from `inner`.

## Tuple strategies

### `Generate.Tuples<T1, T2>`

```csharp
Strategy<(T1, T2)> Generate.Tuples<T1, T2>(Strategy<T1> first, Strategy<T2> second)
Strategy<(T1, T2, T3)> Generate.Tuples<T1, T2, T3>(Strategy<T1>, Strategy<T2>, Strategy<T3>)
Strategy<(T1, T2, T3, T4)> Generate.Tuples<T1, T2, T3, T4>(Strategy<T1>, Strategy<T2>, Strategy<T3>, Strategy<T4>)
```

Generates value tuples from independent strategies. Each element is drawn independently.

## Composition strategies

### `Generate.Compose<T>(Func<IGeneratorContext, T> factory)`

```csharp
Strategy<T> Generate.Compose<T>(Func<IGeneratorContext, T> factory)
```

Imperative strategy builder. The `factory` receives an `IGeneratorContext` and calls `ctx.Generate(strategy)` to draw values.

```csharp
var strategy = Generate.Compose<Person>(ctx =>
{
    string name = ctx.Generate(Generate.Strings(1, 50));
    int age = ctx.Generate(Generate.Integers<int>(0, 150));
    return new Person(name, age);
});
```

`IGeneratorContext` members:
- `T Generate<T>(Strategy<T> strategy)` — draw a value
- `void Assume(bool condition)` — skip current example if false
- `void Target(double observation, string label = "default")` — record a targeting score

### `Generate.Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)`

See [How to generate recursive structures](../how-to/generate-recursive-structures.md).

### `Generate.StateMachine<TMachine, TState, TCommand>(int maxSteps = 50)`

See [How to test stateful systems](../how-to/test-stateful-systems.md).

## LINQ combinators

All combinators are extension methods on `Strategy<T>`.

### `Select<TSource, TResult>(Func<TSource, TResult> selector)`

Maps each generated value. Equivalent to `strategy.map(f)` in Hypothesis.

```csharp
Strategy<string> upperStrings = Generate.Strings().Select(s => s.ToUpperInvariant());
```

### `Where<T>(Func<T, bool> predicate)`

Filters generated values. Use sparingly — see CON101.

```csharp
Strategy<int> evens = Generate.Integers<int>(0, 1000).Where(n => n % 2 == 0);
```

### `SelectMany<TSource, TResult>(Func<TSource, Strategy<TResult>> selector)`

Generates a value, then uses it to create another strategy. Enables dependent generation.

```csharp
Strategy<(List<int>, int)> listWithElement =
    Generate.Lists(Generate.Integers<int>(), minSize: 1)
        .SelectMany(list =>
            Generate.SampledFrom(list).Select(elem => (list, elem)));
```

### `Zip<TFirst, TSecond>(Strategy<TSecond> second)`

```csharp
Strategy<(TFirst, TSecond)> Zip<TFirst, TSecond>(this Strategy<TFirst> first, Strategy<TSecond> second)
Strategy<TResult> Zip<TFirst, TSecond, TResult>(this Strategy<TFirst>, Strategy<TSecond>, Func<TFirst, TSecond, TResult>)
```

Generates a pair from two independent strategies.

### `OrNull<T>()`

```csharp
Strategy<T?> OrNull<T>(this Strategy<T> source)
    where T : struct
```

Wraps the strategy to produce `null` approximately 10% of the time.

### `WithLabel<T>(string label)`

```csharp
Strategy<T> WithLabel<T>(this Strategy<T> source, string label)
```

Tags generated values with `label` for failure output. Multiple strategies can be labelled independently.

## Extension properties

See [Extension properties reference](extension-properties.md) for `.Positive`, `.NonEmpty`, `|` operator, and related conveniences.
