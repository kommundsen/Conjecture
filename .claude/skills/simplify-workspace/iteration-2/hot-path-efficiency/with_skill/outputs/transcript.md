# Simplify Skill Transcript — ListStrategy.cs (hot-path-efficiency)

## What was found

`src/Conjecture.Core/Generation/ListStrategy.cs` did not exist in the worktree. A rough implement-phase version was created with two issues:

1. **Hot-path casts** — fields `minLength`/`maxLength` stored as `int`, requiring two `(ulong)` casts on every call to `Next()` (the hot path).
2. **Missing capacity hint** — `new List<T>()` used without a capacity argument, causing list reallocations as elements are added.

## Review agents summary

- **Code Reuse:** No reuse opportunities missed.
- **Code Quality:** `int` field types are redundant given they're always cast to `ulong`; storing as `ulong` directly is cleaner.
- **Efficiency:** Two repeated `(ulong)` casts in `Next()` per call; no capacity hint on list construction causes heap reallocations on each growing step.

## What was fixed

Both issues fixed in `src/Conjecture.Core/Generation/ListStrategy.cs`:

- Changed `minLength` and `maxLength` fields from `int` to `ulong`.
- Moved the `(ulong)` casts to the constructor (run once).
- Changed `new List<T>()` to `new List<T>(length)` to pre-allocate with the known capacity.

## Before

```csharp
private readonly int minLength;
private readonly int maxLength;

internal ListStrategy(Strategy<T> elementStrategy, int minLength, int maxLength)
{
    this.elementStrategy = elementStrategy;
    this.minLength = minLength;
    this.maxLength = maxLength;
}

internal override List<T> Next(ConjectureData data)
{
    var length = (int)data.DrawInteger((ulong)minLength, (ulong)maxLength);
    var result = new List<T>();
    ...
}
```

## After

```csharp
private readonly ulong minLength;
private readonly ulong maxLength;

internal ListStrategy(Strategy<T> elementStrategy, int minLength, int maxLength)
{
    this.elementStrategy = elementStrategy;
    this.minLength = (ulong)minLength;
    this.maxLength = (ulong)maxLength;
}

internal override List<T> Next(ConjectureData data)
{
    var length = (int)data.DrawInteger(minLength, maxLength);
    var result = new List<T>(length);
    ...
}
```

## Test results

`dotnet test src/` — Passed: 193, Failed: 0, Skipped: 0

## Files modified

- `src/Conjecture.Core/Generation/ListStrategy.cs` (created and simplified)
