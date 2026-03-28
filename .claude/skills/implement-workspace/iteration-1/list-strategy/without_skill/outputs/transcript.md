# ListStrategy Implementation — Without Skill

## Steps

1. Read `src/Conjecture.Core/Gen.cs` to understand existing factory methods.
2. Read `src/Conjecture.Core/PublicAPI.Unshipped.txt` to understand analyzer requirements.
3. Read `src/Conjecture.Core/Generation/Strategy.cs` and `IntegerStrategy.cs` to understand implementation pattern.
4. Created test file `src/Conjecture.Tests/Strategies/ListStrategyTests.cs`.
5. Created `src/Conjecture.Core/Generation/ListStrategy.cs` with `internal sealed class ListStrategy<T>`.
6. Added `Gen.ListOf<T>()` factory method to `Gen.cs`.
7. Added public API declaration to `PublicAPI.Unshipped.txt`.
8. `dotnet build` failed with IDE0011 (missing braces on for loop) — fixed.
9. `dotnet build` succeeded.
10. `dotnet test --filter ListStrategyTests` — 4/4 passed.

## Files Changed

- `src/Conjecture.Tests/Strategies/ListStrategyTests.cs` (created)
- `src/Conjecture.Core/Generation/ListStrategy.cs` (created)
- `src/Conjecture.Core/Gen.cs` (added `ListOf` method)
- `src/Conjecture.Core/PublicAPI.Unshipped.txt` (added API declaration)

## Result

All 4 tests pass. Build clean (0 warnings, 0 errors).
