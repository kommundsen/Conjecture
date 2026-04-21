# Attributes reference

## `[Property]`

Marks a test method as a property-based test. Replaces `[Fact]`, `[Test]`, or `[TestMethod]` depending on the framework.

**Valid targets:** Public instance methods on public test classes.

**Valid return types:** `bool`, `void`, `Task`, `Task<bool>`.

```csharp
[Property]
public bool Addition_is_commutative(int a, int b) => a + b == b + a;

[Property]
public void Reverse_preserves_length(List<int> xs)
{
    Assert.Equal(xs.Count, xs.AsEnumerable().Reverse().Count());
}

[Property]
public async Task<bool> Api_returns_200(int id)
{
    Assume.That(id > 0);
    var response = await _client.GetAsync($"/items/{id}");
    return response.IsSuccessStatusCode;
}
```

**Properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxExamples` | `int` | `100` | Number of examples to generate |
| `Seed` | `ulong` | `0` | `0` = random; any other value pins the seed |
| `UseDatabase` | `bool` | `true` | Persist and replay failures |
| `MaxStrategyRejections` | `int` | `5` | Max consecutive `Where()` rejections |
| `DeadlineMs` | `int` | `0` | `0` = no deadline; otherwise per-example timeout in ms |
| `Targeting` | `bool` | `true` | Enable hill-climbing phase |
| `TargetingProportion` | `double` | `0.5` | Fraction of examples spent on targeting |
| `ExportReproOnFailure` | `bool` | `false` | Write shrunk counterexample to file on failure |
| `ReproOutputPath` | `string` | `".conjecture/repros/"` | Output directory for repro files |

### MTP-specific notes

When using `Conjecture.TestingPlatform`, `[Property]` is defined in that package rather than in the framework adapter. Two differences apply:

**`Seed` type is `ulong?`.** A `null` value means random (the default). In xUnit, NUnit, and MSTest adapters, `Seed` is `ulong` and `0` means random.

```csharp
// Conjecture.TestingPlatform — pin with a non-null value
[Property(Seed = 42UL)]
public bool My_property(int value) => ...;

// Other adapters — 0 means random, any other value pins
[Property(Seed = 42UL)]
public bool My_property(int value) => ...;
```

**CLI overrides take precedence.** The `--conjecture-seed` and `--conjecture-max-examples` options (passed after `--` to `dotnet test`) override the corresponding attribute values for every property in the run. See [How to use the MTP adapter](../how-to/use-mtp-adapter.md#cli-options).

For runner-level configuration options provided by Microsoft Testing Platform itself, see the [MTP documentation](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-intro).

## `[ConjectureSettings]`

Applies settings to a test method, or at assembly level to all tests in the assembly.

```csharp
// Per-test (same effect as [Property] properties):
[Property]
[ConjectureSettings(MaxExamples = 1000, Seed = 42UL)]
public bool My_property(int value) => ...;

// Assembly-level:
[assembly: ConjectureSettings(MaxExamples = 500, UseDatabase = false)]
```

All `ConjectureSettings` record properties are available. See [Reference: Settings](settings.md) for the full table.

## `[From<TProvider>]`

Specifies a custom `IStrategyProvider<T>` for a parameter.

**Valid targets:** `[Property]` method parameters.

```csharp
public sealed class PositiveInts : IStrategyProvider<int>
{
    public Strategy<int> Create() => Generate.Integers<int>(1, int.MaxValue);
}

[Property]
public bool Positive_ints_are_positive([From<PositiveInts>] int value) => value > 0;
```

`TProvider` must implement `IStrategyProvider<T>` where `T` matches the parameter type. The provider must have a public parameterless constructor.

## `[FromFactory]`

Specifies a static factory method on the test class that returns a `Strategy<T>` for a parameter.

**Valid targets:** `[Property]` method parameters.

```csharp
[Property]
public bool Orders_have_items([FromFactory(nameof(CreateOrderStrategy))] Order order)
{
    return order.Items.Count > 0;
}

private static Strategy<Order> CreateOrderStrategy() =>
    from customerId in Generate.Integers<int>(1, 10_000)
    from items in Generate.Lists(Generate.Strings(1, 30), minSize: 1)
    select new Order(customerId, items);
```

The factory method must be `static`, return `Strategy<T>` where `T` matches the parameter type, and take no parameters.

## `[Example]`

Provides explicit test cases that run before generated examples.

**Valid targets:** `[Property]` methods. Can be applied multiple times.

```csharp
[Property]
[Example(0)]
[Example(int.MaxValue)]
[Example(int.MinValue + 1)]
public bool Abs_is_non_negative(int value)
{
    Assume.That(value != int.MinValue);
    return Math.Abs(value) >= 0;
}
```

Multiple `[Example]` attributes are run in declaration order before any generated examples. If an `[Example]` violates the property, the test fails immediately with the explicit value (no shrinking needed).

`[Example]` accepts `params object?[]` — pass one argument per parameter:

```csharp
[Property]
[Example("hello", 5)]
[Example("", 0)]
public bool String_length_matches(string s, int expected)
{
    return s.Length == expected;
}
```

## `[Arbitrary]`

Marks a type for the source generator to derive an `IStrategyProvider<T>` implementation at compile time.

**Valid targets:** Classes, structs, and records with accessible constructors.

```csharp
[Arbitrary]
public partial record Address(string Street, string City, int ZipCode);
```

The source generator emits `AddressArbitrary : IStrategyProvider<Address>` alongside the type. The generated class is `internal sealed` and lives in the same namespace.

Requirements:
- Type must be `partial`
- Must have at least one accessible constructor
- All constructor parameter types must have auto-resolvable strategies

See [How to use source generators](../how-to/use-source-generators.md) for full usage and supported types.

## Gen.For&lt;T&gt;() constraint attributes

Applied to constructor parameters or `init` properties of `[Arbitrary]` types to constrain what the source generator produces. See [Reference: Gen.For&lt;T&gt;()](gen-for.md) for the full attribute reference.

| Attribute | Target | Effect |
|---|---|---|
| `[GenRange(min, max)]` | Numeric parameters | Constrains generated value to [`min`, `max`] |
| `[GenStringLength(min, max)]` | `string` parameters | Constrains generated string length |
| `[GenRegex(pattern)]` | `string` parameters | Constrains generated strings to match the pattern |
| `[GenMaxDepth(n)]` | The type itself | Caps recursive generation depth to `n` |

## `[assembly: ConjectureSettings]`

Applies `ConjectureSettings` to every `[Property]` test in the assembly. See `[ConjectureSettings]` above for properties.

```csharp
// In any .cs file in your test project:
[assembly: ConjectureSettings(MaxExamples = 1000, UseDatabase = false)]
```
