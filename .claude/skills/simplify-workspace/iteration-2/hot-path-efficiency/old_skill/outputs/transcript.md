# ListStrategy.cs Simplification

## Setup

Created files:
- `src/Conjecture.Tests/Strategies/ListStrategyTests.cs`
- `src/Conjecture.Core/Generation/ListStrategy.cs`
- Added `Gen.ListOf<T>` to `src/Conjecture.Core/Gen.cs`
- Added API entry to `src/Conjecture.Core/PublicAPI.Unshipped.txt`

Initial test run (rough implementation): 4/4 passed.

## Review

Three agents examined the rough ListStrategy.cs:

### Agent 1: Code Reuse
No duplicates found. `DrawInteger`, `elementStrategy.Next` — both correctly delegate to existing infrastructure.

### Agent 2: Code Quality
No issues found. No comments, no leaky abstractions, clean field layout. `int` fields with casts to `ulong` in `Next` is minor but consistent with how the DrawInteger API accepts ulongs — not a quality issue.

### Agent 3: Efficiency
**Issue found:** `new List<T>()` allocates with default capacity (4). For lists longer than 4 elements, the internal array is resized (doubled) one or more times — each resize allocates a new array and copies. Since `length` is known before the loop, `new List<T>(length)` pre-allocates the exact capacity and eliminates all internal resizes. This is a hot-path call (invoked on every draw), so the saving is real.

## Fix Applied

```csharp
// Before
var result = new List<T>();

// After
var result = new List<T>(length);
```

File changed: `src/Conjecture.Core/Generation/ListStrategy.cs` only.

## Test Results After Fix

```
Passed!  - Failed: 0, Passed: 197, Skipped: 0, Total: 197, Duration: 74 ms
```

All tests green.
