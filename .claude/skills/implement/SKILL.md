---
name: implement
description: >
  Write the minimal production code to make failing tests pass (TDD Green phase) in the Conjecture .NET project.
  Use this skill whenever the user asks to implement a feature, make tests pass, write production code, or is in the green phase of a TDD cycle — even if they don't say "TDD" or "green phase" explicitly.
  Triggers on phrases like "implement X", "make the tests pass", "write the code for", "make it compile", or after failing tests have been written and production code is needed.
---

Write the minimal production code to make failing tests pass (TDD Green phase).

## Input

Test class or file to target (e.g., `IntegerStrategyTests` or `src/Conjecture.Tests/IntegerStrategyTests.cs`). If omitted, run all failing tests and address them all.

## Steps

1. Run `dotnet test src/ --filter "FullyQualifiedName~<target>"` (or `dotnet test src/` if no target) to see which tests fail and why.
2. Read the failing test(s) to understand the exact behavior required.
3. Identify the production project from the test file path using the mapping in CLAUDE.md. Read the relevant production file(s) there (or create them if they don't exist). Check `docs/decisions/` for ADRs that constrain the design.
4. Write the **minimum** code that makes the failing tests pass:
   - No speculative features, no extra overloads
   - Hardcode constants only if a single test demands it; generalise only when a second test forces you to
   - Follow existing patterns in the codebase (file-scoped namespaces, `readonly struct` where appropriate, etc.)
   - Any new or changed `public` API surface in non-test projects must be declared in that project's `PublicAPI.Unshipped.txt` (see CLAUDE.md). The project enforces this via RS0016 at build time — the compiler error includes the exact signature string to copy in.
   - Follow the **Code Style Quick Reference** below — these rules are enforced as build warnings.
5. Run `dotnet build src/ 2>&1 | grep -E 'warning (IDE|CS)'` — fix **all** warnings before continuing. Common culprits: block-scoped namespace, `var`, missing braces, `new T()` instead of `new()`, explicit `if`/`return` instead of ternary.
6. Run `dotnet test src/ --filter "FullyQualifiedName~<target>"` again — all targeted tests must be green.
7. Run `dotnet test src/` — no previously passing tests may be broken.
8. Report: files changed, tests now passing, and any design choices made.

## Guidelines

- Implement only what the tests demand — resist the urge to anticipate future tests
- If making a test pass requires a design decision (e.g., choosing an algorithm), record it with `/decision` before implementing
- Keep methods short; if a method exceeds ~20 lines, note it for the Refactor phase (`/simplify`)
- Do not add `public` API surface beyond what the tests reference

## Code Style Quick Reference

Rules enforced as build warnings — write them correctly the first time:

| Pattern | Correct | Wrong |
|---|---|---|
| Namespace | `namespace Foo.Bar;` | `namespace Foo.Bar { }` |
| `var` | never — always explicit types | `var x = new Foo()` |
| Object creation | `new()` when type is apparent | `new Foo()` on right side of assignment |
| Braces | always, even for single-line `if`/`for` | `if (x) return;` |
| `using` | `using var x = …;` (simple) | `using (var x = …) { }` |
| `null` check | `x is null` / `x is not null` | `x == null` / `x != null` |
| Null propagation | `x?.Foo` | `x == null ? null : x.Foo` |
| Ternary assign | `x = cond ? a : b;` | `if (cond) x = a; else x = b;` |
| Ternary return | `return cond ? a : b;` | `if (cond) return a; return b;` |
| Switch | `x switch { … }` | `switch (x) { … }` when all arms return |
| Index | `arr[^1]` | `arr[arr.Length - 1]` |
| Primary ctor | `class Foo(int x)` | `class Foo { Foo(int x) { _x = x; } }` |
| Expression prop | `int X => _x;` | `int X { get { return _x; } }` |
| Expression method | block body `{ return …; }` | `=>` (methods must use block bodies) |
| Pattern matching | `x is Foo f` | `x is Foo; var f = (Foo)x` |
| `not` pattern | `x is not null` | `!(x is null)` |
