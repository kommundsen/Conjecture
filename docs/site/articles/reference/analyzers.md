# Roslyn analyzers reference

Conjecture includes Roslyn analyzers bundled in `Conjecture.Core`. They activate automatically — no additional packages needed.

## Diagnostic rules

### CON100: Assertion inside `[Property]` method

**Severity:** Warning  
**Target:** `void`-returning `[Property]` methods that use framework assertions.

Consider returning `bool` for simpler, clearer tests.

```csharp
// Triggers CON100:
[Property]
public void Bad(int value)
{
    Assert.True(value >= 0);  // CON100
}

// Preferred:
[Property]
public bool Good(int value) => value >= 0;
```

### CON101: High-rejection `Where()` predicate

**Severity:** Warning  
**Target:** `.Where()` predicates likely to reject most generated values.

High rejection rates cause poor performance and risk `UnsatisfiedAssumptionException`.

```csharp
// Triggers CON101:
var primes = Generate.Integers<int>(0, 1_000_000).Where(IsPrime);

// Better: generate from a small known set
var primes = Generate.SampledFrom(new[] { 2, 3, 5, 7, 11, 13 });
```

### CON102: Sync-over-async inside `[Property]`

**Severity:** Info  
**Target:** `.Result`, `.GetAwaiter().GetResult()`, or `.Wait()` inside a `[Property]` method.

Make the method `async` instead.

```csharp
// Triggers CON102:
[Property]
public bool Bad(int id)
{
    var result = _client.GetAsync($"/api/{id}").Result;  // CON102
    return result.IsSuccessStatusCode;
}

// Better:
[Property]
public async Task<bool> Good(int id)
{
    var result = await _client.GetAsync($"/api/{id}");
    return result.IsSuccessStatusCode;
}
```

### CON103: Strategy bounds are inverted

**Severity:** Error  
**Target:** Strategy factory calls where `min > max`.

```csharp
// Triggers CON103:
Generate.Integers<int>(100, 0)  // min (100) > max (0)

// Fix:
Generate.Integers<int>(0, 100)
```

### CON104: `Assume.That(false)` always skips

**Severity:** Warning  
**Target:** Unconditional `Assume.That(false)` calls.

Every example is skipped — the test is effectively disabled.

```csharp
// Triggers CON104:
Assume.That(false);  // test is useless
```

### CON105: `[Arbitrary]` provider exists but `[From<T>]` not used

**Severity:** Info  
**Target:** Parameters whose type has an `[Arbitrary]`-generated provider, but `[From<T>]` is absent.

```csharp
[Arbitrary]
public partial record Money(decimal Amount, string Currency);

// Triggers CON105:
[Property]
public bool Test(Money money) => ...;

// Better:
[Property]
public bool Test([From<MoneyArbitrary>] Money money) => ...;
```

## Source generator diagnostics

The `[Arbitrary]` source generator reports its own set of diagnostics:

| ID | Severity | Description |
|---|---|---|
| CON200 | Error | No accessible constructor found on `[Arbitrary]` type |
| CON201 | Error | `[Arbitrary]` type is not `partial` |
| CON202 | Warning | Constructor parameter type has no resolvable strategy |

## Suppressing diagnostics

Per-location:

```csharp
#pragma warning disable CON100
[Property]
public void Intentionally_using_assertions(int value) { /* ... */ }
#pragma warning restore CON100
```

Project-wide via `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.CON100.severity = none
```
