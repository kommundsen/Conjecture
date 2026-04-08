---
name: simplify
description: >
  Review changed code for reuse, quality, and efficiency, then fix any issues found — the TDD Refactor phase for the Conjecture .NET project.
  Use this skill whenever the user wants to clean up code after making tests pass, refactor a file, review for code smells, or is in the refactor phase of a TDD cycle — even if they don't say "refactor" or "simplify" explicitly.
  Triggers on phrases like "clean this up", "refactor", "simplify", "review for issues", "any code smells?", or automatically after the implement skill completes a green phase.
---

Review changed code for reuse, quality, and efficiency, then fix any issues found.

## Input

One or more production file paths to review (e.g., `src/Conjecture.Core/Strategies/FooStrategy.cs`). If omitted, runs `git diff` to find changed files automatically.

## Steps

1. **Identify changes**
   - If arguments provided: read those files directly.
   - Otherwise: run `git diff HEAD` to find changed production files.

2. **Spawn the `reviewer` agent** — pass the full diff or file contents and any available test results. Use `subagent_type: "reviewer"`.

3. **Fix issues** — based on the reviewer's findings. Before applying, check whether the user's ask was narrow or broad:
   - **Broad ask** ("simplify", "refactor", "any issues?", "clean this up"): apply all legitimate findings.
   - **Narrow ask** ("clean up the comments", "just fix X"): apply only findings that match the stated scope. Note other findings as observations but don't apply them.

4. **Verify** — run `dotnet build src/ 2>&1 | grep -E 'warning (IDE|CS)'` to catch style violations, then run `dotnet test src/` to confirm tests still pass.

5. **Report** — summarise what was fixed, or confirm the code was already clean.

## Guidelines

- Only fix issues in the files under review — do not refactor surrounding code.
- Do not add features or anticipate future requirements.
- If a finding conflicts with an ADR in `docs/decisions/`, the ADR wins — note it and skip the fix.
