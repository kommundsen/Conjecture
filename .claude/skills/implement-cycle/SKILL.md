---
name: implement-cycle
description: >
  Execute the next incomplete TDD cycle from the Conjecture implementation plan: Red → Green → Refactor → Verify → Mark done.
  Use this skill whenever the user wants to work through the next planned cycle, progress the implementation plan, run the next TDD iteration, or says "next cycle" — even if they don't specify which one.
  Triggers on phrases like "do the next cycle", "implement cycle X.Y.Z", "work through phase 2", "continue the implementation plan", or "what's the next step in the plan".
---

Execute the next incomplete TDD cycle from the implementation plan: Red → Green → Refactor → Verify → Mark done.

## Input

Optional cycle specifier:
- Omitted: finds the first cycle with unchecked items across all `docs/IMPLEMENTATION-PLAN-PHASE-*.md` files
- Cycle number (e.g. `1.1.1`): targets that specific cycle
- Phase number (e.g. `2`): restricts search to Phase 2 plan

## Steps

1. **Find the cycle**
   - Read `docs/IMPLEMENTATION-PLAN-PHASE-*.md` files in phase order.
   - Locate the target cycle: first `#### Cycle X.Y.Z` block with any `- [ ]` items (or the specified cycle).
   - Extract: cycle number, test file path, implement target, behavior description (sub-bullets under the `/test` line).
   - If the cycle spec includes a `/decision` step first, invoke the `decision` skill before proceeding.

2. **Red phase — write failing tests**
   - Invoke the `test` skill with the behavior description and test file path from the cycle spec.
   - Run `dotnet build src/` — must fail or have test failures (red). If unexpectedly green, stop and report.

3. **Green phase — implement**
   - Invoke the `implement` skill with the test class name extracted from the test file path.
   - Run `dotnet test src/ --filter "FullyQualifiedName~<TestClassName>"`.
   - If tests fail, invoke `implement` again with the failing test output as additional context. Repeat until all targeted tests pass or 3 attempts have been made. If still failing after 3 attempts, stop and report what remains failing.

4. **Refactor phase — simplify**
   - Invoke the `simplify` skill on the production files created or modified during the Green phase.
   - Run `dotnet test src/ --filter "FullyQualifiedName~<TestClassName>"` again — must still be green.

5. **Verify no regressions**
   - Run `dotnet test src/` — full suite must be green.
   - If any previously-passing test now fails: stop, report the regression, and do NOT proceed to step 6.

6. **PublicAPI check**
   - If the cycle's implement bullet mentions "Update `PublicAPI.Unshipped.txt`", verify the file was updated with the new public symbols. If not, update it now.

7. **Mark cycle complete**
   - In the plan file, change every `- [ ]` line within this cycle's block to `- [x]`.

8. **Report**
   - Cycle number completed, test file created, production files created/modified, test count, refactor changes made, any design decisions recorded.

9. **Suggest commit**
   - Invoke the `commit-message` skill to generate a suggested commit message for all changes made during this cycle.
   - Ask the user: "Want me to commit with this message?"
   - If yes, stage all new and modified files from this cycle and commit with the suggested message (no `Co-Authored-By` trailer).
   - If no, do nothing — the user will commit manually.

## Guidelines

- One cycle per invocation — do not cascade into the next cycle.
- If the cycle spec references a decision before implementing, invoke the `decision` skill first.
- Never mark done if build or tests are red.
- Scope all changes to what the cycle spec demands.
