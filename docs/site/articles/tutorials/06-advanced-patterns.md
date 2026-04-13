# Tutorial 6: Advanced Patterns

This tutorial covers advanced Conjecture features: source generators, the example database, assembly-level settings, and testing patterns.

## Source Generators

The source generator (bundled in `Conjecture.Core`) auto-derives strategies for your types at compile time.

Mark a type with `[Arbitrary]`:

```csharp
using Conjecture.Core;

[Arbitrary]
public partial record Address(string Street, string City, int ZipCode);
```

The source generator creates `AddressArbitrary : IStrategyProvider<Address>` using auto-resolved strategies for each constructor parameter. Use it:

```csharp
[Property]
public bool Addresses_have_street([From<AddressArbitrary>] Address addr)
{
    return addr.Street is not null;
}
```

Requirements:
- The type must be `partial`
- Must have an accessible constructor
- All constructor parameter types must have resolvable strategies

See [How to use source generators](../how-to/use-source-generators.md) for details.

## Roslyn Analyzers

Roslyn analyzers are bundled in `Conjecture.Core` and active automatically. Diagnostics:

| ID | Description |
|---|---|
| CON100 | Assertion inside `void` `[Property]` — consider returning `bool` |
| CON101 | `Where()` predicate likely rejects most values |
| CON102 | Sync-over-async (`.Result`, `.GetAwaiter().GetResult()`) inside `[Property]` |
| CON103 | Strategy bounds are inverted (`min > max`) |
| CON104 | `Assume.That(false)` always skips |
| CON105 | `[Arbitrary]` provider exists but `[From<T>]` not used |
| CON107 | Non-deterministic operation in `[Property]` (e.g. `Guid.NewGuid()`, `DateTime.Now`) |
| CON108 | `Assume.That` condition always true given strategy constraint |
| CON109 | No strategy found for `[Property]` parameter type |
| CON110 | Async `[Property]` method contains no `await` |
| CON111 | `Target.Maximize`/`Minimize` outside `[Property]` method |
| CJ0050 | Suggest named extension property (`.Positive`, `.NonEmpty`) instead of `.Where()` |

## The Example Database

When a test fails, Conjecture stores the failing byte buffer in a local SQLite database. Next time the test runs, it replays that input first — guaranteeing the bug is caught immediately without waiting for random generation to rediscover it.

The database lives at `DatabasePath` (default: `.conjecture/examples/`).

### Workflow

1. Test fails → failing example saved to database
2. You fix the bug
3. Test passes → example stays in database as a regression test
4. Future runs replay stored examples before generating new ones

### CI Considerations

The example database is a local file. In CI, you can:
- **Commit it** — `.conjecture/examples/` in your repo ensures CI replays known failures
- **Disable it** — `[assembly: ConjectureSettings(UseDatabase = false)]`

## Assembly-Level Settings

Override defaults for all tests in an assembly:

```csharp
[assembly: ConjectureSettings(MaxExamples = 500, DatabasePath = ".test-db/")]
```

Per-test `[Property]` attributes take precedence over assembly-level settings.

## Explicit Examples

`[Example]` runs specific inputs before generated ones:

```csharp
[Property]
[Example("")]
[Example("  ")]
[Example("normal input")]
public bool Trim_handles_edge_cases(string input)
{
    var trimmed = input.Trim();
    return trimmed.Length <= input.Length;
}
```

Use this for:
- Known edge cases (empty, null, boundary values)
- Regression tests from past failures
- Documentation by example

## Async Properties

Property methods can be `async`:

```csharp
[Property]
public async Task<bool> Api_returns_valid_response(int id)
{
    Assume.That(id > 0);
    var response = await _client.GetAsync($"/api/items/{id}");
    return response.StatusCode != System.Net.HttpStatusCode.InternalServerError;
}
```

## Testing Patterns

### Roundtrip / Serialization

```csharp
[Property]
public bool Json_roundtrip(MyType value)
{
    var json = JsonSerializer.Serialize(value);
    var deserialized = JsonSerializer.Deserialize<MyType>(json);
    return value == deserialized;
}
```

### Invariants

```csharp
[Property]
public bool Set_union_contains_both(
    List<int> a,
    List<int> b)
{
    var union = new HashSet<int>(a);
    union.UnionWith(b);
    return a.All(union.Contains) && b.All(union.Contains);
}
```

### Oracle / Reference Implementation

```csharp
[Property]
public bool Custom_sort_matches_linq(List<int> items)
{
    var expected = items.OrderBy(x => x).ToList();
    var actual = MySort.Sort(items);
    return expected.SequenceEqual(actual);
}
```

### Idempotence

```csharp
[Property]
public bool Normalize_is_idempotent(string input)
{
    var once = Normalize(input);
    var twice = Normalize(once);
    return once == twice;
}
```

## Further Reading

- [Reference: Settings](../reference/settings.md) — all settings
- [How to use source generators](../how-to/use-source-generators.md) — `[Arbitrary]` in depth
- [Reference: Analyzers](../reference/analyzers.md) — all diagnostic rules
- <xref:Conjecture.Core?text=API+Reference> — generated docs
