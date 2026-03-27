Review changed code for reuse, quality, and efficiency, then fix any issues found.

## Input

$ARGUMENTS — one or more production file paths to review (e.g., `src/Conjecture.Core/Strategies/FooStrategy.cs`). If omitted, runs `git diff` to find changed files automatically.

## Steps

1. **Identify changes**
   - If arguments provided: read those files directly.
   - Otherwise: run `git diff HEAD` to find changed production files.

2. **Launch three review agents in parallel** — pass each agent the full diff or file contents.

   ### Agent 1: Code Reuse
   - Search for existing utilities or helpers that could replace newly written code.
   - Flag any new function that duplicates existing functionality.
   - Flag inline logic that could use an existing utility.

   ### Agent 2: Code Quality
   - Redundant state (duplicates existing state, could be derived)
   - Parameter sprawl (adding params instead of restructuring)
   - Copy-paste with slight variation (should be unified)
   - Leaky abstractions (exposing internals, breaking encapsulation)
   - Stringly-typed code (raw strings where constants/enums exist)
   - Unnecessary comments (comments explaining WHAT, not WHY — delete these; keep only non-obvious WHY)

   ### Agent 3: Efficiency
   - Unnecessary work (redundant computations, repeated reads)
   - Missed concurrency (independent ops run sequentially)
   - Hot-path bloat (new blocking work in per-draw or per-request paths)
   - Memory issues (unbounded structures, missing cleanup, leaks)
   - Overly broad operations (reading all when only part is needed)

3. **Fix issues** — aggregate all agent findings and apply fixes directly. Skip false positives; note why they're skipped.

4. **Verify** — run `dotnet test src/` to confirm tests still pass after any changes.

5. **Report** — summarise what was fixed, or confirm the code was already clean.

## Guidelines

- Only fix issues in the files under review — do not refactor surrounding code
- Do not add features or anticipate future requirements
- If a finding conflicts with an ADR in `docs/decisions/`, the ADR wins — note it and skip the fix
