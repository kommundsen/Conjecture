# Eval 1 With Skill — Transcript

**Prompt:** Add a test to IntegerStrategyTests.cs for when min > max is passed to Gen.Integers<int>() — I expect it to throw ArgumentOutOfRangeException

## Step 1: Read the skill
Read SKILL.md — TDD Red phase skill. Key instructions: identify target class, determine test file location (add to existing if tests exist), write tests covering happy path + edge cases + failure, run `dotnet build` — must fail.

## Step 2: Identify target and file
- Target: `Gen.Integers<int>()` → `IntegerStrategy<T>` in `src/Conjecture.Core/Generation/IntegerStrategy.cs`
- Test file already exists: `src/Conjecture.Tests/Strategies/IntegerStrategyTests.cs`
- Action: add to existing file

## Step 3: Read production code
`IntegerStrategy<T>` constructor has no validation — no guard for `min > max`. The call `Gen.Integers<int>(10, 5)` will not throw; the test will be red.

## Step 4: Write test
Added to `IntegerStrategyTests.cs`:
```csharp
[Fact]
public void Integers_MinGreaterThanMax_ThrowsArgumentOutOfRangeException()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => Gen.Integers<int>(10, 5));
}
```

## Step 5: Verify red phase
Ran: `dotnet test src/ --filter "FullyQualifiedName~IntegerStrategyTests"`

Result:
```
[xUnit.net 00:00:00.18] Conjecture.Tests.Strategies.IntegerStrategyTests.Integers_MinGreaterThanMax_ThrowsArgumentOutOfRangeException [FAIL]
Error Message: Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.ArgumentOutOfRangeException)
Failed! - Failed: 1, Passed: 6, Skipped: 0, Total: 7
```

**RED PHASE CONFIRMED** ✓

## Summary
- Added 1 test to existing `IntegerStrategyTests.cs`
- Method name: `Integers_MinGreaterThanMax_ThrowsArgumentOutOfRangeException` (follows convention)
- Assertion: `Assert.Throws<ArgumentOutOfRangeException>`
- Test calls `Gen.Integers<int>(10, 5)` with min > max
- Build passes but test fails — genuine red phase
