# Extension properties reference

Convenience properties and operators on `Strategy<T>` values that filter or combine strategies without writing a `Where` call.

## Integer extension properties

These properties are available on `Strategy<int>` (and other signed integer strategy types).

### `.Positive`

Filters to values ≥ 0.

```csharp
Strategy<int> positiveInts = Strategy.Integers<int>().Positive;
// Equivalent to: Strategy.Integers<int>().Where(x => x >= 0)
```

### `.Negative`

Filters to values < 0.

```csharp
Strategy<int> negativeInts = Strategy.Integers<int>().Negative;
// Equivalent to: Strategy.Integers<int>().Where(x => x < 0)
```

### `.NonZero`

Filters to values ≠ 0.

```csharp
Strategy<int> nonZeroInts = Strategy.Integers<int>().NonZero;
// Equivalent to: Strategy.Integers<int>().Where(x => x != 0)
```

> [!TIP]
> For `Positive` and `NonZero`, prefer `Strategy.Integers<int>(1, int.MaxValue)` or similar bounded ranges — they generate more efficiently than filtering the full range.

## String extension properties

### `.NonEmpty`

Filters to non-empty strings.

```csharp
Strategy<string> nonEmpty = Strategy.Strings().NonEmpty;
// Equivalent to: Strategy.Strings().Where(s => s.Length > 0)
// Better alternative: Strategy.Strings(minLength: 1)
```

> [!TIP]
> `Strategy.Strings(minLength: 1)` is more efficient than `.NonEmpty` because it never generates empty strings in the first place.

## Collection extension properties

### `.NonEmpty` on `Strategy<List<T>>`

Filters to non-empty lists.

```csharp
Strategy<List<int>> nonEmptyList = Strategy.Lists(Strategy.Integers<int>()).NonEmpty;
// Equivalent to: Strategy.Lists(...).Where(xs => xs.Count > 0)
// Better alternative: Strategy.Lists(Strategy.Integers<int>(), minSize: 1)
```

## `|` operator — inline `OneOf`

The `|` operator combines two strategies into a `OneOf`, picking one at random per example:

```csharp
Strategy<int> smallOrLarge = Strategy.Integers<int>(0, 10) | Strategy.Integers<int>(990, 1000);
// Equivalent to: Strategy.OneOf(Strategy.Integers<int>(0, 10), Strategy.Integers<int>(990, 1000))
```

Chain for more alternatives:

```csharp
Strategy<string> keywords =
    Strategy.Just("true") |
    Strategy.Just("false") |
    Strategy.Just("null");
```

> [!NOTE]
> The `|` operator always gives equal probability to each operand, regardless of how many alternatives are chained. This is the same semantics as `Strategy.OneOf`.
