# Roslyn Analyzers

Conjecture includes Roslyn analyzers (bundled in `Conjecture.Core`) that provide compile-time diagnostics catching common property-test mistakes. They activate automatically — no additional packages are needed.

## Diagnostic Rules

### CON100: Assertion Inside `[Property]` Method

**Severity:** Warning

Fires when a `void`-returning `[Property]` method uses assertions (like `Assert.Equal`). Consider returning `bool` instead for simpler tests.

```csharp
// Triggers CON100:
[Property]
public void Bad(int value)
{
    Assert.True(value >= 0);  // CON100: consider returning bool instead
}

// Better:
[Property]
public bool Good(int value) => value >= 0;
```

### CON101: High-Rejection `Where()` Predicate

**Severity:** Warning

Fires when a `.Where()` predicate is likely to reject most generated values, causing poor performance or `UnsatisfiedAssumptionException`.

```csharp
// Triggers CON101:
var primes = Generate.Integers<int>(0, 1_000_000)
    .Where(IsPrime);  // Most values aren't prime

// Better: use a dedicated strategy or generate smaller ranges
```

### CON102: Sync-Over-Async Inside `[Property]`

**Severity:** Info

Fires when `.Result`, `.GetAwaiter().GetResult()`, or `.Wait()` is used inside a `[Property]` method. Make the method `async` instead.

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

### CON103: Strategy Bounds Are Inverted

**Severity:** Error

Fires when `min` is greater than `max` in a strategy factory call.

```csharp
// Triggers CON103:
Generate.Integers<int>(100, 0)  // min (100) > max (0)

// Fix:
Generate.Integers<int>(0, 100)
```

### CON104: `Assume.That(false)` Always Skips

**Severity:** Warning

Fires when `Assume.That(false)` is called unconditionally, causing every example to be filtered.

```csharp
// Triggers CON104:
Assume.That(false);  // Every example is skipped — test is useless
```

### CON105: `[Arbitrary]` Provider Exists but `[From<T>]` Not Used

**Severity:** Info

Fires when a parameter's type has an `[Arbitrary]`-generated provider available, but the parameter doesn't use `[From<T>]` to opt in.

```csharp
[Arbitrary]
public partial record Money(decimal Amount, string Currency);

// Triggers CON105:
[Property]
public bool Test(Money money)  // CON105: use [From<MoneyArbitrary>]
{
    return money.Amount >= 0;
}

// Better:
[Property]
public bool Test([From<MoneyArbitrary>] Money money)
{
    return money.Amount >= 0;
}
```

## Suppressing Diagnostics

Suppress individual diagnostics with `#pragma` or `.editorconfig`:

```csharp
#pragma warning disable CON100
[Property]
public void Intentionally_using_assertions(int value) { /* ... */ }
#pragma warning restore CON100
```

Or in `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.CON100.severity = none
```
