# Conjecture Assumptions and Filtering

## `Assume.That(bool condition)`

Discard a generated example when the condition is false. Use when the property only applies to a subset of the input space.

```csharp
[Property]
public void Division_IsExact(int numerator, int denominator)
{
    Assume.That(denominator != 0);     // skip divide-by-zero cases
    Assume.That(numerator % denominator == 0);  // only exact divisions

    var result = numerator / denominator;
    Assert.Equal(numerator, result * denominator);
}
```

`Assume.That` throws `UnsatisfiedAssumptionException` internally — the test framework catches it and counts the example as filtered (not failed).

## `IGeneratorContext.Assume(bool)` (inside `Generate.Compose`)

Same semantics, but available inside an imperative strategy:

```csharp
Generate.Compose(ctx =>
{
    var n = ctx.Generate(Generate.Integers<int>());
    ctx.Assume(n > 0);   // discard non-positive draws
    return new PositiveWrapper(n);
});
```

## Rejection Budget

Conjecture allows up to `MaxStrategyRejections` (default: 200) consecutive rejections before raising `ConjectureException`. This prevents test suites from hanging on impossible or extremely tight filters.

If you hit the budget:
- Your filter may be **too restrictive** — most generated values are being discarded
- Prefer to restructure the strategy to generate valid values directly

```csharp
// Bad: high rejection rate
[Property]
public void Test(int x)
{
    Assume.That(x is > 0 and < 5); // rejects 99.9% of int range
    ...
}

// Better: use a bounded strategy
[Property]
public void Test([From<BoundedStrategy>] int x)
{
    ...
}

// Or inline with Compose:
// Generate.Integers<int>(1, 4)
```

## `UnsatisfiedAssumptionException`

Thrown by `Assume.That(false)`. You should not catch this exception in test code — let the framework handle it. If you see it in stack traces, it's a sign that the framework didn't intercept it properly (e.g., swallowed by a catch-all in application code).
