# Strategies reference

All factory methods on the `Generate` static class, plus LINQ combinators from `StrategyExtensions`.

## Numeric strategies

### `Strategy.Integers<T>()`

```csharp
Strategy<T> Strategy.Integers<T>()
    where T : IBinaryInteger<T>
```

Generates values across the full range of `T`. Works with `int`, `long`, `byte`, `short`, `uint`, `ulong`, `sbyte`, `ushort`, `nint`, `nuint`.

### `Strategy.Integers<T>(T min, T max)`

```csharp
Strategy<T> Strategy.Integers<T>(T min, T max)
    where T : IBinaryInteger<T>
```

Generates values in `[min, max]` inclusive. `min` must be ≤ `max`.

### `Strategy.Doubles()`

```csharp
Strategy<double> Strategy.Doubles()
```

Generates any `double`, including `NaN`, `+Infinity`, `-Infinity`, and denormals.

### `Strategy.Doubles(double min, double max)`

```csharp
Strategy<double> Strategy.Doubles(double min, double max)
```

Generates `double` values in `[min, max]`. Neither bound may be `NaN`.

### `Strategy.Floats()`

```csharp
Strategy<float> Strategy.Floats()
```

Generates any `float`, including `NaN`, `+Infinity`, `-Infinity`, and denormals.

### `Strategy.Floats(float min, float max)`

```csharp
Strategy<float> Strategy.Floats(float min, float max)
```

Generates `float` values in `[min, max]`. Neither bound may be `NaN`.

### `Strategy.Booleans()`

```csharp
Strategy<bool> Strategy.Booleans()
```

Generates `true` and `false` with equal probability.

## String strategies

See [String strategies reference](string-strategies.md) for `Strings`, `Identifiers`, `NumericStrings`, and `VersionStrings`.

## Date and time strategies

### `Strategy.DateTimeOffsets()`

```csharp
Strategy<DateTimeOffset> Strategy.DateTimeOffsets()
```

Generates `DateTimeOffset` values across the full range.

### `Strategy.DateTimeOffsets(DateTimeOffset min, DateTimeOffset max)`

```csharp
Strategy<DateTimeOffset> Strategy.DateTimeOffsets(DateTimeOffset min, DateTimeOffset max)
```

Generates `DateTimeOffset` values in `[min, max]`.

### `Strategy.TimeSpans()`

```csharp
Strategy<TimeSpan> Strategy.TimeSpans()
```

Generates `TimeSpan` values across the full range.

### `Strategy.TimeSpans(TimeSpan min, TimeSpan max)`

```csharp
Strategy<TimeSpan> Strategy.TimeSpans(TimeSpan min, TimeSpan max)
```

Generates `TimeSpan` values in `[min, max]`.

### `Strategy.DateOnlyValues()`

```csharp
Strategy<DateOnly> Strategy.DateOnlyValues()
```

Generates `DateOnly` values across the full range.

### `Strategy.DateOnlyValues(DateOnly min, DateOnly max)`

```csharp
Strategy<DateOnly> Strategy.DateOnlyValues(DateOnly min, DateOnly max)
```

Generates `DateOnly` values in `[min, max]`.

### `Strategy.TimeOnlyValues()`

```csharp
Strategy<TimeOnly> Strategy.TimeOnlyValues()
```

Generates `TimeOnly` values across the full range.

### `Strategy.TimeOnlyValues(TimeOnly min, TimeOnly max)`

```csharp
Strategy<TimeOnly> Strategy.TimeOnlyValues(TimeOnly min, TimeOnly max)
```

Generates `TimeOnly` values in `[min, max]`.

For boundary-focused extensions (`.NearMidnight()`, `.NearDstTransition()`, etc.) and `Strategy.TimeZones`/`Strategy.ClockSet` factory methods, see [Time strategies reference](time-strategies.md).

## Byte buffer strategies

### `Strategy.FromBytes<T>(ReadOnlySpan<byte> buffer)`

```csharp
Strategy<T> Strategy.FromBytes<T>(ReadOnlySpan<byte> buffer)
```

Replays a value of type `T` from a fixed byte buffer using the default strategy for `T`. The buffer is the same format stored by the example database. Useful for deterministic replay and round-trip testing.

## Collection strategies

### `Strategy.Arrays<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<T[]> Strategy.Arrays<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
```

Generates `T[]` with length in `[minSize, maxSize]`. `minSize` must be ≥ 0 and ≤ `maxSize`. For a fixed-size array, set `minSize == maxSize`.

### `Strategy.Lists<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<List<T>> Strategy.Lists<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
```

Generates `List<T>` with length in `[minSize, maxSize]`. `minSize` must be ≥ 0 and ≤ `maxSize`.

### `Strategy.Sets<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<IReadOnlySet<T>> Strategy.Sets<T>(Strategy<T> inner, int minSize = 0, int maxSize = 100)
```

Generates `IReadOnlySet<T>` (backed by `HashSet<T>`) with cardinality in `[minSize, maxSize]`.

### `Strategy.Dictionaries<TKey, TValue>(Strategy<TKey> keys, Strategy<TValue> values, int minSize = 0, int maxSize = 100)`

```csharp
Strategy<IReadOnlyDictionary<TKey, TValue>> Strategy.Dictionaries<TKey, TValue>(
    Strategy<TKey> keys,
    Strategy<TValue> values,
    int minSize = 0,
    int maxSize = 100)
```

Generates `IReadOnlyDictionary<TKey, TValue>` with entry count in `[minSize, maxSize]`.

## Choice strategies

### `Strategy.Just<T>(T value)`

```csharp
Strategy<T> Strategy.Just<T>(T value)
```

Always produces the same `value`. Useful as a degenerate case in `OneOf` or `Recursive`.

### `Strategy.OneOf<T>(params Strategy<T>[] strategies)`

```csharp
Strategy<T> Strategy.OneOf<T>(params Strategy<T>[] strategies)
```

Picks one strategy per example uniformly at random, then generates a value from it.

### `Strategy.SampledFrom<T>(IReadOnlyList<T> values)`

```csharp
Strategy<T> Strategy.SampledFrom<T>(IReadOnlyList<T> values)
```

Picks one element from `values` uniformly at random. `values` must be non-empty.

### `Strategy.Enums<T>()`

```csharp
Strategy<T> Strategy.Enums<T>()
    where T : struct, Enum
```

Picks one declared value from `T` uniformly at random.

### `Strategy.Nullable<T>(Strategy<T> inner)`

```csharp
Strategy<T?> Strategy.Nullable<T>(Strategy<T> inner)
    where T : struct
```

Returns `null` approximately 10% of the time; otherwise generates a value from `inner`.

## Tuple strategies

### `Strategy.Tuples<T1, T2>`

```csharp
Strategy<(T1, T2)> Strategy.Tuples<T1, T2>(Strategy<T1> first, Strategy<T2> second)
Strategy<(T1, T2, T3)> Strategy.Tuples<T1, T2, T3>(Strategy<T1>, Strategy<T2>, Strategy<T3>)
Strategy<(T1, T2, T3, T4)> Strategy.Tuples<T1, T2, T3, T4>(Strategy<T1>, Strategy<T2>, Strategy<T3>, Strategy<T4>)
```

Generates value tuples from independent strategies. Each element is drawn independently.

## Composition strategies

### `Strategy.Compose<T>(Func<IGeneratorContext, T> factory)`

```csharp
Strategy<T> Strategy.Compose<T>(Func<IGeneratorContext, T> factory)
```

Imperative strategy builder. The `factory` receives an `IGeneratorContext` and calls `ctx.Generate(strategy)` to draw values.

```csharp
var strategy = Strategy.Compose<Person>(ctx =>
{
    string name = ctx.Generate(Strategy.Strings(1, 50));
    int age = ctx.Generate(Strategy.Integers<int>(0, 150));
    return new Person(name, age);
});
```

`IGeneratorContext` members:
- `T Generate<T>(Strategy<T> strategy)` — draw a value
- `void Assume(bool condition)` — skip current example if false
- `void Target(double observation, string label = "default")` — record a targeting score

### `Strategy.Recursive<T>(Strategy<T> baseCase, Func<Strategy<T>, Strategy<T>> recursive, int maxDepth = 5)`

See [How to generate recursive structures](../how-to/generate-recursive-structures.md).

### `Strategy.StateMachine<TMachine, TState, TCommand>(int maxSteps = 50)`

See [How to test stateful systems](../how-to/test-stateful-systems.md).

## LINQ combinators

All combinators are extension methods on `Strategy<T>`.

### `Select<TSource, TResult>(Func<TSource, TResult> selector)`

Maps each generated value. Equivalent to `strategy.map(f)` in Hypothesis.

```csharp
Strategy<string> upperStrings = Strategy.Strings().Select(s => s.ToUpperInvariant());
```

### `Where<T>(Func<T, bool> predicate)`

Filters generated values. Use sparingly — see CON101.

```csharp
Strategy<int> evens = Strategy.Integers<int>(0, 1000).Where(n => n % 2 == 0);
```

### `SelectMany<TSource, TResult>(Func<TSource, Strategy<TResult>> selector)`

Generates a value, then uses it to create another strategy. Enables dependent generation.

```csharp
Strategy<(List<int>, int)> listWithElement =
    Strategy.Lists(Strategy.Integers<int>(), minSize: 1)
        .SelectMany(list =>
            Strategy.SampledFrom(list).Select(elem => (list, elem)));
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
