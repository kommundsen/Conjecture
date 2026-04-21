# Gen.For&lt;T&gt;() reference

API surface, attribute reference, primitive mapping, and diagnostic codes for `Generate.For<T>()` and the constraint attributes.

## `Generate.For<T>()`

**Namespace:** `Conjecture.Core`  
**Package:** `Conjecture.Core` (bundled — no extra NuGet package)

```csharp
// Returns the registered strategy for T.
public static Strategy<T> For<T>();

// Returns the registered strategy for T with property overrides applied.
public static Strategy<T> For<T>(Action<ForConfiguration<T>> configure);
```

`T` must be decorated with `[Arbitrary]`. If no provider is registered, a **CON312** error is raised at the call site.

## `ForConfiguration<T>`

Passed to the `configure` callback in `Generate.For<T>(cfg => ...)`.

### `Override<TProp>`

```csharp
public ForConfiguration<T> Override<TProp>(
    Expression<Func<T, TProp>> selector,
    Strategy<TProp> strategy);
```

Replaces the strategy for the property identified by `selector` with `strategy`. Multiple overrides can be chained; all other properties use their generated defaults.

```csharp
Strategy<Order> orders = Generate.For<Order>(cfg => cfg
    .Override(o => o.Total, Generate.Decimals(0m, 999.99m))
    .Override(o => o.Customer, Generate.Strings(minLength: 1)));
```

**Constraints:**
- `selector` must be a simple member-access expression (e.g., `o => o.Total`). Nested access and method calls are not supported.
- The `TProp` of the override must match the property's declared type exactly.

## Constraint attributes

Constraint attributes are applied to constructor parameters or `init` properties of `[Arbitrary]` types. The source generator reads them at compile time.

### `[GenRange]`

```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class GenRangeAttribute(double min, double max) : Attribute
```

Constrains the generated range for a numeric parameter to [`min`, `max`]. Applies to `int`, `long`, `short`, `byte`, `uint`, `ulong`, `ushort`, `sbyte`, `float`, `double`, and `decimal`.

```csharp
[Arbitrary]
public partial record Product(
    [GenRange(0.01, 9_999.99)] decimal Price,
    [GenRange(1, 500)] int Quantity);
```

### `[GenStringLength]`

```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class GenStringLengthAttribute(int minLength, int maxLength) : Attribute
```

Constrains the length of generated strings to [`minLength`, `maxLength`].

```csharp
[Arbitrary]
public partial record Customer(
    [GenStringLength(1, 100)] string Name,
    [GenStringLength(5, 254)] string Email);
```

### `[GenRegex]`

```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class GenRegexAttribute(string pattern) : Attribute
```

Constrains generated strings to match `pattern`. Requires the `Conjecture.Regex` package.

```csharp
[Arbitrary]
public partial record PhoneNumber(
    [GenRegex(@"^\+\d{7,15}$")] string Value);
```

If `Conjecture.Regex` is not referenced, the generator emits **CON202** and falls back to unconstrained strings.

### `[GenMaxDepth]`

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class GenMaxDepthAttribute(int maxDepth) : Attribute
```

Applied to the **type** (not a parameter) to cap recursive generation depth. Required on self-referential or mutually recursive types; optional on non-recursive types.

```csharp
[Arbitrary]
[GenMaxDepth(4)]
public partial class TreeNode
{
    public TreeNode(int Value, TreeNode? Left, TreeNode? Right) { /* ... */ }
}
```

At depth 0, nullable recursive parameters produce `null` (leaf nodes). At depth > 0, recursion continues up to the cap.

Default depth when the attribute is absent: **5**.

## Primitive type → default strategy mapping

| Parameter type | Generated strategy |
|---|---|
| `bool` | `Generate.Booleans()` |
| `byte`, `short`, `int`, `long` | `Generate.Integers<T>()` (full range) |
| `uint`, `ulong`, `ushort`, `sbyte` | `Generate.Integers<T>()` (full range) |
| `float` | `Generate.Floats()` |
| `double` | `Generate.Doubles()` |
| `decimal` | `Generate.Decimals()` |
| `string` | `Generate.Strings()` (length 0–20, printable ASCII) |
| `char` | `Generate.Chars()` |
| `Guid` | `Generate.Guids()` |
| `DateTime` | `Generate.DateTimes()` |
| `DateTimeOffset` | `Generate.DateTimeOffsets()` |
| `DateOnly` | `Generate.DateOnlyValues()` |
| `TimeOnly` | `Generate.TimeOnlyValues()` |
| `TimeSpan` | `Generate.TimeSpans()` |
| `T?` (value type) | `Generate.Nullable(inner)` |
| `T?` (reference type) | `inner.OrNull()` (~10% null probability) |
| Enum `T` | `Generate.Enums<T>()` |
| `[Arbitrary]` type | `Generate.For<T>()` (nested) |

`[GenRange]`, `[GenStringLength]`, and `[GenRegex]` override the defaults for the individual parameter they are applied to.

## Supported collection types

| Parameter type | Generated strategy |
|---|---|
| `List<T>` | `Generate.Lists(inner)` |
| `IReadOnlyList<T>` | `Generate.Lists(inner)` |
| `IList<T>` | `Generate.Lists(inner)` |
| `IEnumerable<T>` | `Generate.Lists(inner)` |
| `T[]` | `Generate.Lists(inner).Select(l => l.ToArray())` |
| `IReadOnlySet<T>`, `HashSet<T>` | `Generate.Sets(inner)` |
| `IReadOnlyDictionary<K,V>`, `Dictionary<K,V>` | `Generate.Dictionaries(keyInner, valueInner)` |

Collection size defaults to 0–100 elements. Apply `[GenRange]` to a `List<T>` parameter to constrain the element count.

## `Generate.For<T>()` call-site diagnostics

These diagnostics fire at the `Generate.For<T>()` or `[From<T>]` call site, not on the type declaration.

### CON310 — target is an interface

**Severity:** Error

`Generate.For<T>()` cannot generate a strategy for an interface type. Apply `[Arbitrary]` to each concrete implementation and use `Generate.OneOf(...)` to combine them.

```csharp
// Error:
Strategy<IShape> shapes = Generate.For<IShape>();  // CON310

// Fix: use concrete types
Strategy<IShape> shapes = Generate.OneOf(
    Generate.For<Circle>().Select(c => (IShape)c),
    Generate.For<Rectangle>().Select(r => (IShape)r));
```

### CON311 — abstract type with no `[Arbitrary]` subtypes

**Severity:** Error

The type is abstract but has no concrete subtypes decorated with `[Arbitrary]` in the compilation.

```csharp
// Error:
[Arbitrary]
public abstract partial class Animal { }

Strategy<Animal> animals = Generate.For<Animal>();  // CON311

// Fix: add [Arbitrary] to at least one concrete subtype
[Arbitrary]
public partial class Dog : Animal { }
```

### CON312 — no registered provider

**Severity:** Error

`Generate.For<T>()` was called for a type that has no `[Arbitrary]` attribute and therefore no registered `IStrategyProvider<T>`.

```csharp
// Error:
public class Widget { }
Strategy<Widget> widgets = Generate.For<Widget>();  // CON312

// Fix: add [Arbitrary] and make the type partial
[Arbitrary]
public partial class Widget { }
```

### CON313 — mutual recursion without `[GenMaxDepth]`

**Severity:** Warning

Two or more `[Arbitrary]` types reference each other without either having `[GenMaxDepth]`. Generation may be very deep or diverge.

```csharp
// Warning — CON313:
[Arbitrary]
public partial record Parent(Child? Child);

[Arbitrary]
public partial record Child(Parent? Parent);

// Fix: add [GenMaxDepth] to at least one side
[Arbitrary]
[GenMaxDepth(3)]
public partial record Parent(Child? Child);
```

## See also

- [How to use Gen.For&lt;T&gt;()](../how-to/use-gen-for.md) — step-by-step recipes
- [Understanding Gen.For&lt;T&gt;() source generation](../explanation/gen-for-source-generator.md) — design rationale and registry mechanics
- [Reference: Analyzers](analyzers.md) — CON200–CON202, CON205, CON300–CON302 (type-declaration diagnostics)
- [Reference: Attributes](attributes.md) — `[Arbitrary]`, `[From<T>]`, `[FromFactory]`
