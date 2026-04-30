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

### CON107: Non-deterministic operation in `[Property]`

**Severity:** Warning  
**Target:** Calls to `Guid.NewGuid()`, `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Random.Shared`, `new Random()`, `Environment.TickCount`, or `Environment.TickCount64` inside a `[Property]` method.

Non-deterministic operations break shrink reproducibility.

```csharp
// Triggers CON107:
[Property]
public bool Bad(int x)
{
    Guid id = Guid.NewGuid();  // CON107
    return id != Guid.Empty;
}

// Better: inject randomness via a strategy parameter
[Property]
public bool Good([From<GuidStrategy>] Guid id) => id != Guid.Empty;
```

### CON108: Redundant `Assume.That` given strategy constraint

**Severity:** Warning  
**Target:** `Assume.That(condition)` where the condition is always satisfied by a known built-in strategy (`PositiveInts`, `NegativeInts`, `NonNegativeInts`).

```csharp
// Triggers CON108:
[Property]
public bool Bad([From<PositiveInts>] int x)
{
    Assume.That(x > 0);  // CON108: always true for PositiveInts
    return x * 2 > x;
}

// Fix: remove the redundant assumption
[Property]
public bool Good([From<PositiveInts>] int x) => x * 2 > x;
```

### CON109: No strategy found for `[Property]` parameter

**Severity:** Warning  
**Target:** `[Property]` method parameters whose type has no resolvable strategy — no built-in support, no `[From<T>]`, and no `[Arbitrary]` on the type.

```csharp
// Triggers CON109:
[Property]
public bool Bad(MyCustomType x) => x is not null;  // CON109

// Fix: add [Arbitrary] to the type or use [From<T>]
[Property]
public bool Good([From<MyCustomTypeStrategy>] MyCustomType x) => x is not null;
```

### CON110: Async `[Property]` without `await`

**Severity:** Info  
**Target:** `[Property]` methods declared `async` that contain no `await` expression.

```csharp
// Triggers CON110:
[Property]
public async Task<bool> Bad(int x) { return x > 0; }  // CON110

// Fix: remove async or add an awaited call
[Property]
public bool Good(int x) => x > 0;
```

### CON111: `Target.Maximize`/`Minimize` outside `[Property]`

**Severity:** Warning  
**Target:** Calls to `Target.Maximize(…)` or `Target.Minimize(…)` in a method not decorated with `[Property]`. These calls are no-ops outside a property test body.

```csharp
// Triggers CON111:
public void Helper(double x)
{
    Target.Maximize(x);  // CON111: no effect here
}

// Fix: move the call into a [Property] method
[Property]
public bool Good(double x)
{
    Target.Maximize(x);
    return x < 1000;
}
```

### CJ0050: Suggest named extension property

**Severity:** Info  
**Target:** `.Where()` predicates that match a named extension property: `.Positive`, `.Negative`, `.NonZero`, `.NonEmpty`.

A code fix is available.

```csharp
// Triggers CJ0050:
var pos = Generate.Integers<int>().Where(x => x > 0);  // CJ0050

// Fix: use the extension property
var pos = Generate.Integers<int>().Positive;
```

## Source generator diagnostics

The `[Arbitrary]` source generator reports its own set of diagnostics:

### Concrete type diagnostics (CON200–CON202)

| ID | Severity | Description |
|---|---|---|
| CON200 | Error | No accessible constructor found on `[Arbitrary]` type |
| CON201 | Error | `[Arbitrary]` type is not `partial` |
| CON202 | Warning | Constructor parameter type has no resolvable strategy |

### `Generate.For<T>()` call-site diagnostics (CON310–CON313)

These fire at `Generate.For<T>()` or `[From<T>]` call sites. See [Reference: Generate.For&lt;T&gt;()](generate-for.md#generatefort-call-site-diagnostics) for resolution steps.

| ID | Severity | Description |
|---|---|---|
| CON310 | Error | `Generate.For<T>()` target is an interface |
| CON311 | Error | `Generate.For<T>()` target is abstract with no `[Arbitrary]` subtypes |
| CON312 | Error | `Generate.For<T>()` has no registered provider — type lacks `[Arbitrary]` |
| CON313 | Warning | Mutually recursive `[Arbitrary]` types without `[StrategyMaxDepth]` |

### Sealed hierarchy diagnostics (CON205, CON300–CON302)

These fire when `[Arbitrary]` is applied to an abstract class (hierarchy mode).

| ID | Severity | Description |
|---|---|---|
| CON205 | Warning | Concrete subtype of `[Arbitrary]`-decorated abstract base lacks `[Arbitrary]`; it will not be included in the generated `OneOf` strategy |
| CON300 | Error | `[Arbitrary]` base type is not abstract |
| CON301 | Error | `[Arbitrary]` base type is not a class or record (interfaces are not supported) |
| CON302 | Error | No concrete `[Arbitrary]`-decorated subtypes found in compilation |

> [!NOTE]
> The generator only detects subtypes in the same compilation. Subtypes defined in external assemblies are silently excluded — CON205 does not fire for them. See [Understanding sealed hierarchy strategies](../explanation/sealed-hierarchy-strategies.md#the-same-compilation-constraint) for details.

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
