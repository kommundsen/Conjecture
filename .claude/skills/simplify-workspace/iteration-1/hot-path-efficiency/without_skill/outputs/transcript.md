# ListStrategy.cs Review & Simplification

## Setup

Created files:
- `src/Conjecture.Tests/Strategies/ListStrategyTests.cs`
- `src/Conjecture.Core/Generation/ListStrategy.cs`
- Added `Gen.ListOf<T>` to `src/Conjecture.Core/Gen.cs`
- Added API entry to `src/Conjecture.Core/PublicAPI.Unshipped.txt`

Initial test run: 4/4 passed.

## Issues Found

1. **Missing capacity hint** — `new List<T>()` allocates with default capacity (4), causing up to 3 internal array reallocations for lists of 5–10 elements. Since `length` is known before the loop, `new List<T>(length)` eliminates all resizes.

2. **Attempted removal** — Tried removing braces from `for` loop body to reduce noise, but the project enforces `IDE0011` (always add braces), so this was reverted immediately after the build error.

## Fix Applied

```csharp
// Before
var result = new List<T>();

// After
var result = new List<T>(length);
```

## Test Results After Fix

```
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: 30 ms
```

All tests green.
