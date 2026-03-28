# Eval 1 Without Skill — Transcript (Baseline)

**Prompt:** Add a test to IntegerStrategyTests.cs for when min > max is passed to Gen.Integers<int>() — I expect it to throw ArgumentOutOfRangeException

## Step 1: Locate the test file
Found `src/Conjecture.Tests/Strategies/IntegerStrategyTests.cs` — existing file.

## Step 2: Write test (no skill guidance)
Added to `IntegerStrategyTests.cs`:
```csharp
[Fact]
public void Integers_InvalidRange_ThrowsException()
{
    Assert.Throws<ArgumentOutOfRangeException>(() => Gen.Integers<int>(10, 5));
}
```

Notes on differences from with-skill version:
- Method name is `Integers_InvalidRange_ThrowsException` — shorter, less specific about condition vs expected result
- Did not check red phase before writing (no explicit "must verify it fails" guidance)
- Did not look at production code first to confirm no guard exists

## Step 3: Build result
Test fails — red phase confirmed (same underlying reason: no validation in IntegerStrategy).

```
Failed! - Failed: 1, Passed: 6, Total: 7
Assert.Throws() Failure: No exception was thrown
```
