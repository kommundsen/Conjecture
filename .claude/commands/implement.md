Write the minimal production code to make failing tests pass (TDD Green phase).

## Input

$ARGUMENTS — test class or file to target (e.g., `IntegerStrategyTests` or `src/Conjecture.Tests/IntegerStrategyTests.cs`). If omitted, run all failing tests and address them all.

## Steps

1. Run `dotnet test src/ --filter "FullyQualifiedName~<target>"` (or `dotnet test src/` if no target) to see which tests fail and why.
2. Read the failing test(s) to understand the exact behavior required.
3. Read the relevant production file(s) in `src/Conjecture.Core/` (or create them if they don't exist). Check `docs/decisions/` for ADRs that constrain the design.
4. Write the **minimum** code that makes the failing tests pass:
   - No speculative features, no extra overloads
   - Hardcode constants only if a single test demands it; generalise only when a second test forces you to
   - Follow existing patterns in the codebase (file-scoped namespaces, `readonly struct` where appropriate, etc.)
5. Run `dotnet test src/ --filter "FullyQualifiedName~<target>"` again — all targeted tests must be green.
6. Run `dotnet test src/` — no previously passing tests may be broken.
7. Report: files changed, tests now passing, and any design choices made.

## Guidelines

- Implement only what the tests demand — resist the urge to anticipate future tests
- If making a test pass requires a design decision (e.g., choosing an algorithm), record it with `/decision` before implementing
- Keep methods short; if a method exceeds ~20 lines, note it for the Refactor phase (`/simplify`)
- Do not add `public` API surface beyond what the tests reference
