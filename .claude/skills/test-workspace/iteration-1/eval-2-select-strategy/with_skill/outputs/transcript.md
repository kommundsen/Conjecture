# Eval 2 With Skill — Transcript

**Prompt:** Write tests for the existing SelectStrategy — Gen.Integers<int>(0, 10).Select(x => x * 2) should always return even numbers in [0, 20], and an identity selector should return values in the same range as the source

## Step 1: Read the skill
Read SKILL.md — TDD Red phase skill. Key instructions: check existing types, create new test file if no existing tests, verify red phase.

## Step 2: Check existing types and test location
- `SelectStrategy<TSource, TResult>` exists at `src/Conjecture.Core/Generation/SelectStrategy.cs`
- No `SelectStrategyTests.cs` in `src/Conjecture.Tests/Strategies/` — create new file
- `Select()` extension method is in `Conjecture.Core.Generation.StrategyExtensions`

## Step 3: Write tests
Created `src/Conjecture.Tests/Strategies/SelectStrategyTests.cs`:
- `Select_DoublingSelector_ReturnsEvenNumbersInRange` — verifies x*2 results are in [0,20] and even
- `Select_IdentitySelector_ReturnsValuesInSourceRange` — verifies identity keeps range [0,10]
- File-scoped namespace: `Conjecture.Tests.Strategies`
- Added `using Conjecture.Core.Generation` for `StrategyExtensions`

## Step 4: Red phase verification
Ran: `dotnet test src/ --filter "FullyQualifiedName~SelectStrategyTests"`

Initial attempt failed compilation: `CS1061: 'Strategy<int>' does not contain a definition for 'Select'`
→ Fixed by adding `using Conjecture.Core.Generation;`

Result after fix:
```
Passed! - Failed: 0, Passed: 2, Total: 2
```

**GREEN (not red)** — `SelectStrategy` is already fully implemented. The skill's step 4 says: "If they pass, the tests are not testing anything new; revise them." However, this is a case of writing coverage for an already-implemented type — the tests ARE useful, just not strictly TDD red-phase. The skill doesn't clearly handle this scenario.

## Summary
- Created new file `src/Conjecture.Tests/Strategies/SelectStrategyTests.cs` ✓
- Namespace: `Conjecture.Tests.Strategies` ✓
- Naming convention: `Select_DoublingSelector_ReturnsEvenNumbersInRange` ✓
- Tests verify correct transform behavior ✓
- Build result: GREEN (tests pass — implementation already exists)
- **Issue:** Skill's "must be red" requirement conflicts with the prompt asking to test an existing type
