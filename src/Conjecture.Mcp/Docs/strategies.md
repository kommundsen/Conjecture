# Conjecture Strategies API Reference

## Factory Methods (`Generate.*`)

| Method | Returns | Notes |
|--------|---------|-------|
| `Generate.Booleans()` | `Strategy<bool>` | Uniform true/false |
| `Generate.Integers<T>()` | `Strategy<T>` | Full range; T : IBinaryInteger<T>, IMinMaxValue<T> |
| `Generate.Integers<T>(min, max)` | `Strategy<T>` | Bounded; T : IBinaryInteger<T> |
| `Generate.Floats()` | `Strategy<float>` | Includes NaN, ±Infinity |
| `Generate.Floats(min, max)` | `Strategy<float>` | Bounded range |
| `Generate.Doubles()` | `Strategy<double>` | Includes NaN, ±Infinity |
| `Generate.Doubles(min, max)` | `Strategy<double>` | Bounded range |
| `Generate.Strings(minLength, maxLength, minCodepoint, maxCodepoint, alphabet)` | `Strategy<string>` | All params optional; default: printable ASCII, length 0–20 |
| `Generate.Text(minLength, maxLength)` | `Strategy<string>` | Alias for Strings() |
| `Generate.Bytes(size)` | `Strategy<byte[]>` | Fixed-length byte array |
| `Generate.Just<T>(value)` | `Strategy<T>` | Always returns the given value |
| `Generate.OneOf<T>(strategies[])` | `Strategy<T>` | Picks uniformly from strategies |
| `Generate.SampledFrom<T>(values)` | `Strategy<T>` | Picks uniformly from a fixed list |
| `Generate.Enums<T>()` | `Strategy<T>` | T : struct, Enum |
| `Generate.Nullable<T>(inner)` | `Strategy<T?>` | T : struct; ~10% null probability |
| `Generate.Tuples(s1, s2)` | `Strategy<(T1,T2)>` | 2-element tuple |
| `Generate.Tuples(s1, s2, s3)` | `Strategy<(T1,T2,T3)>` | 3-element tuple |
| `Generate.Tuples(s1, s2, s3, s4)` | `Strategy<(T1,T2,T3,T4)>` | 4-element tuple |
| `Generate.Lists<T>(inner, minSize, maxSize)` | `Strategy<List<T>>` | Default: 0–100 elements |
| `Generate.Sets<T>(inner, minSize, maxSize)` | `Strategy<IReadOnlySet<T>>` | Unique elements; default: 0–100 |
| `Generate.Dictionaries<K,V>(keyStrategy, valueStrategy, minSize, maxSize)` | `Strategy<IReadOnlyDictionary<K,V>>` | Unique keys |
| `Generate.DateTimeOffsets()` | `Strategy<DateTimeOffset>` | Full range |
| `Generate.DateTimeOffsets(min, max)` | `Strategy<DateTimeOffset>` | Bounded range |
| `Generate.TimeSpans()` | `Strategy<TimeSpan>` | Full range |
| `Generate.TimeSpans(min, max)` | `Strategy<TimeSpan>` | Bounded range |
| `Generate.DateOnlyValues()` | `Strategy<DateOnly>` | Full range |
| `Generate.DateOnlyValues(min, max)` | `Strategy<DateOnly>` | Bounded range |
| `Generate.TimeOnlyValues()` | `Strategy<TimeOnly>` | Full range |
| `Generate.TimeOnlyValues(min, max)` | `Strategy<TimeOnly>` | Bounded range |
| `Generate.Identifiers(minPrefixLength, maxPrefixLength, minDigits, maxDigits)` | `Strategy<string>` | Identifier strings (`[a-z]+\d+`); all params optional |
| `Generate.NumericStrings(minDigits, maxDigits, prefix, suffix)` | `Strategy<string>` | Numeric strings with optional prefix/suffix |
| `Generate.VersionStrings(maxMajor, maxMinor, maxPatch)` | `Strategy<string>` | Version strings (`MAJOR.MINOR.PATCH`) |
| `Generate.FromBytes<T>(buffer)` | `Strategy<T>` | Replay values from a fixed byte buffer |
| `Generate.Recursive<T>(baseCase, recursive, maxDepth)` | `Strategy<T>` | Tree-shaped / self-referential types; default maxDepth 5 |
| `Generate.Compose<T>(factory)` | `Strategy<T>` | Imperative composition via `IGenerationContext` |
| `Generate.StateMachine<TMachine, TState, TCommand>(maxSteps)` | `Strategy<StateMachineRun<TState>>` | Stateful testing |

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

## `DataGen` (standalone data generation)

Use `DataGen` to generate values outside property tests:

| Method | Returns | Notes |
|--------|---------|-------|
| `DataGen.Sample<T>(strategy, count, seed?)` | `IReadOnlyList<T>` | Materialize `count` values |
| `DataGen.SampleOne<T>(strategy, seed?)` | `T` | Single value |
| `DataGen.Stream<T>(strategy, count, seed?)` | `IEnumerable<T>` | Lazy enumeration of `count` values |

## `IGenerationContext` (inside `Generate.Compose`)

```csharp
Generate.Compose(ctx =>
{
    int n = ctx.Generate(Generate.Integers<int>(1, 100));
    string s = ctx.Generate(Generate.Strings());
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
        Generate.Compose(ctx => new MyType(
            ctx.Generate(Generate.Integers<int>()),
            ctx.Generate(Generate.Strings())));
}
```

Once declared, `MyType` parameters in `[Property]` tests are resolved automatically.
