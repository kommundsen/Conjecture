# Conjecture Strategies API Reference

## Factory Methods (`Strategy.*`)

| Method | Returns | Notes |
|--------|---------|-------|
| `Strategy.Booleans()` | `Strategy<bool>` | Uniform true/false |
| `Strategy.Integers<T>()` | `Strategy<T>` | Full range; T : IBinaryInteger<T>, IMinMaxValue<T> |
| `Strategy.Integers<T>(min, max)` | `Strategy<T>` | Bounded; T : IBinaryInteger<T> |
| `Strategy.Floats()` | `Strategy<float>` | Includes NaN, ±Infinity |
| `Strategy.Floats(min, max)` | `Strategy<float>` | Bounded range |
| `Strategy.Doubles()` | `Strategy<double>` | Includes NaN, ±Infinity |
| `Strategy.Doubles(min, max)` | `Strategy<double>` | Bounded range |
| `Strategy.Strings(minLength, maxLength, minCodepoint, maxCodepoint, alphabet)` | `Strategy<string>` | All params optional; default: printable ASCII, length 0–20 |
| `Strategy.Text(minLength, maxLength)` | `Strategy<string>` | Alias for Strings() |
| `Strategy.Arrays<T>(inner, minSize, maxSize)` | `Strategy<T[]>` | Variable-length array; for byte arrays use `Strategy.Arrays(Strategy.Integers<byte>(), minSize, maxSize)` |
| `Strategy.Just<T>(value)` | `Strategy<T>` | Always returns the given value |
| `Strategy.OneOf<T>(strategies[])` | `Strategy<T>` | Picks uniformly from strategies |
| `Strategy.SampledFrom<T>(values)` | `Strategy<T>` | Picks uniformly from a fixed list |
| `Strategy.Enums<T>()` | `Strategy<T>` | T : struct, Enum |
| `Strategy.Nullable<T>(inner)` | `Strategy<T?>` | T : struct; ~10% null probability |
| `Strategy.Tuples(s1, s2)` | `Strategy<(T1,T2)>` | 2-element tuple |
| `Strategy.Tuples(s1, s2, s3)` | `Strategy<(T1,T2,T3)>` | 3-element tuple |
| `Strategy.Tuples(s1, s2, s3, s4)` | `Strategy<(T1,T2,T3,T4)>` | 4-element tuple |
| `Strategy.Lists<T>(inner, minSize, maxSize)` | `Strategy<List<T>>` | Default: 0–100 elements |
| `Strategy.Sets<T>(inner, minSize, maxSize)` | `Strategy<IReadOnlySet<T>>` | Unique elements; default: 0–100 |
| `Strategy.Dictionaries<K,V>(keyStrategy, valueStrategy, minSize, maxSize)` | `Strategy<IReadOnlyDictionary<K,V>>` | Unique keys |
| `Strategy.DateTimeOffsets()` | `Strategy<DateTimeOffset>` | Full range |
| `Strategy.DateTimeOffsets(min, max)` | `Strategy<DateTimeOffset>` | Bounded range |
| `Strategy.TimeSpans()` | `Strategy<TimeSpan>` | Full range |
| `Strategy.TimeSpans(min, max)` | `Strategy<TimeSpan>` | Bounded range |
| `Strategy.DateOnlyValues()` | `Strategy<DateOnly>` | Full range |
| `Strategy.DateOnlyValues(min, max)` | `Strategy<DateOnly>` | Bounded range |
| `Strategy.TimeOnlyValues()` | `Strategy<TimeOnly>` | Full range |
| `Strategy.TimeOnlyValues(min, max)` | `Strategy<TimeOnly>` | Bounded range |
| `Strategy.Indices(int maxValue)` | `Strategy<Index>` | Forward and from-end indices valid for a collection of `maxValue` length |
| `Strategy.Ranges(int maxValue)` | `Strategy<Range>` | Ranges valid for a collection of `maxValue` length |
| `Strategy.Identifiers(minPrefixLength, maxPrefixLength, minDigits, maxDigits)` | `Strategy<string>` | Identifier strings (`[a-z]+\d+`); all params optional |
| `Strategy.NumericStrings(minDigits, maxDigits, prefix, suffix)` | `Strategy<string>` | Numeric strings with optional prefix/suffix |
| `Strategy.VersionStrings(maxMajor, maxMinor, maxPatch)` | `Strategy<string>` | Version strings (`MAJOR.MINOR.PATCH`) |
| `Strategy.FromBytes<T>(buffer)` | `Strategy<T>` | Replay values from a fixed byte buffer |
| `Strategy.Recursive<T>(baseCase, recursive, maxDepth)` | `Strategy<T>` | Tree-shaped / self-referential types; default maxDepth 5 |
| `Strategy.Compose<T>(factory)` | `Strategy<T>` | Imperative composition via `IGenerationContext` |
| `Strategy.StateMachine<TMachine, TState, TCommand>(maxSteps)` | `Strategy<StateMachineRun<TState>>` | Stateful testing |

## LINQ Combinators (extension methods on `Strategy<T>`)

| Method | Returns | Notes |
|--------|---------|-------|
| `.Select(f)` | `Strategy<U>` | Map values |
| `.Where(pred)` | `Strategy<T>` | Filter values (use sparingly — prefer `Assume.That`) |
| `.SelectMany(f)` | `Strategy<U>` | Flatmap / monadic bind |
| `.Zip(other)` | `Strategy<(T,U)>` | Combine two strategies |
| `.Zip(other, selector)` | `Strategy<R>` | Combine with custom merge |
| `.OrNull()` | `Strategy<T?>` | Reference types: ~10% null |
| `.WithLabel(label)` | `Strategy<T>` | Name the strategy for counterexample output |

## Extension Properties

| Property / Operator | Applies to | Returns | Notes |
|---------------------|-----------|---------|-------|
| `.Positive` | `Strategy<int>` | `Strategy<int>` | Filters to values > 0 |
| `.Negative` | `Strategy<int>` | `Strategy<int>` | Filters to values < 0 |
| `.NonZero` | `Strategy<int>` | `Strategy<int>` | Filters to values ≠ 0 |
| `.NonEmpty` | `Strategy<string>` | `Strategy<string>` | Filters to non-empty strings |
| `.NonEmpty` | `Strategy<List<T>>` | `Strategy<List<T>>` | Filters to non-empty lists |
| `\|` | `Strategy<T>` | `Strategy<T>` | Union: `stratA \| stratB` picks from either |

## Sampling extensions (standalone data generation)

Use the extension methods on `Strategy<T>` to generate values outside property tests:

| Method | Returns | Notes |
|--------|---------|-------|
| `strategy.Sample()` | `T` | Single value, fresh seed |
| `strategy.Sample(count)` | `IReadOnlyList<T>` | Materialize `count` values, fresh seed |
| `strategy.Stream()` | `IEnumerable<T>` | Unbounded lazy enumeration |
| `strategy.Stream(count)` | `IEnumerable<T>` | Lazy enumeration of `count` values |
| `strategy.WithSeed(seed)` | `SeededStrategy<T>` | Returns a deterministic view; chain `.Sample()` / `.Stream()` |

## `IGenerationContext` (inside `Strategy.Compose`)

```csharp
Strategy.Compose(ctx =>
{
    int n = ctx.Generate(Strategy.Integers<int>(1, 100));
    string s = ctx.Generate(Strategy.Strings());
    ctx.Assume(n > 0); // equivalent to Assume.That
    return new MyType(n, s);
})
```

## `IStrategyProvider<T>` (source generator)

```csharp
[Arbitrary]
public partial class MyTypeStrategies : IStrategyProvider<MyType>
{
    public static Strategy<MyType> GetStrategy() =>
        Strategy.Compose(ctx => new MyType(
            ctx.Generate(Strategy.Integers<int>()),
            ctx.Generate(Strategy.Strings())));
}
```

Once declared, `MyType` parameters in `[Property]` tests are resolved automatically.