---
name: simplify
description: >
  Review changed code for reuse, quality, and efficiency, then fix any issues found — the TDD Refactor phase for the Conjecture .NET project.
  Triggers: "clean this up", "refactor", "simplify", "review for issues", "any code smells?",
  or automatically after the implement skill completes a green phase.
---

Review changed code for reuse, quality, and efficiency, then fix any issues found.

## Input

One or more production file paths to review. If omitted, runs `git diff` to find changed files.

## Steps

1. **Identify changes** — if arguments provided, read those files. Otherwise run `git diff HEAD` to find changed production files.
2. **Spawn the `reviewer` agent** — pass the full diff or file contents and any available test results.
3. **Fix issues** — based on the reviewer's findings:
   - **Broad ask** ("simplify", "refactor", "any issues?"): apply all legitimate findings
   - **Narrow ask** ("just fix X"): apply only matching findings, note others as observations
4. **Verify** — run `dotnet test src/` to confirm tests still pass.
5. **Report** — summarise what was fixed, or confirm the code was already clean.

## Guidelines

- Only fix issues in the files under review — do not refactor surrounding code
- Do not add features or anticipate future requirements
- If a finding conflicts with an ADR in `docs/decisions/`, the ADR wins — note it and skip
