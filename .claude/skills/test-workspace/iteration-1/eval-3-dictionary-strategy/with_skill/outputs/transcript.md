# Eval 3 With Skill — Transcript

**Prompt:** I want tests for a new DictionaryStrategy — Gen.Dictionary(keyGen, valueGen, minCount: 0, maxCount: 5) should produce dictionaries where all keys are unique and counts are within [0, 5]. The type doesn't exist yet.

## Step 1: Read the skill
TDD Red phase skill. Key for this case: "Do NOT create stub/fake implementations to make them compile; use #pragma warning disable or // TODO: implement comments if the type doesn't exist yet"

## Step 2: Verify type doesn't exist
Searched `src/Conjecture.Core/` — no `DictionaryStrategy.cs`, no `Gen.Dictionary` method. Confirmed non-existent.

## Step 3: Determine test file location
New class `DictionaryStrategy` → `src/Conjecture.Tests/Strategies/DictionaryStrategyTests.cs`

## Step 4: Write tests
Created tests with `#pragma warning disable CS0246` for missing type:
- `Dictionary_CountWithinBounds` — Assert.InRange(dict.Count, 0, 5)
- `Dictionary_AllKeysAreUnique` — dict.Keys.Distinct().Count() == dict.Count
- `Dictionary_MinCountZero_CanProduceEmptyDictionary` — probabilistic check for empty dict
- `Dictionary_MaxCount_NeverExceeded` — Assert.True(count <= 5)

## Step 5: Verify red phase
Build output:
```
error CS0117: 'Gen' does not contain a definition for 'Dictionary'
Build FAILED.
```

Note: Initial attempt had IDE0011 (missing braces in for loop) which this project treats as error at build time. Fixed the style issue while keeping tests red due to missing type.

**RED PHASE CONFIRMED** ✓ — build fails because Gen.Dictionary doesn't exist

## Summary
- New file created: `src/Conjecture.Tests/Strategies/DictionaryStrategyTests.cs` ✓
- Used `#pragma warning disable CS0246` as directed by skill (no stub implementation) ✓
- Tests cover: count bounds, key uniqueness, empty dictionary case, max count ✓
- Build fails: `CS0117: 'Gen' does not contain a definition for 'Dictionary'` ✓
