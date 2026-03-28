---
name: test
description: >
  Write failing xUnit tests for a behavior before implementation exists (TDD Red phase) in the Conjecture .NET project.
  Use this skill whenever the user wants NEW tests written — they describe a behavior to verify, say "write tests for X", "add coverage for X", "test that X does Y", "I need a failing test", or name a strategy/feature that needs tests.
  Triggers even without "TDD" or "red phase" language, as long as the goal is creating tests rather than acting on existing ones.
  Do NOT trigger when the user wants to make existing failing tests pass (use implement), fix build errors in test files (use implement), refactor or reorganize existing tests (use simplify), or simply run/explain tests.
---

Write failing xUnit tests for a behavior before implementation exists (TDD Red phase).

## Input

The behavior to test, e.g. `IntegerStrategy generates values within [min, max]`.

## Steps

1. Identify the target class/component from the description. Check `src/Conjecture.Core/` for existing types; check `docs/decisions/` for relevant ADRs.
2. Determine the test file location:
   - New class `Foo` → `src/Conjecture.Tests/FooTests.cs`
   - Module `Bar/Baz` → `src/Conjecture.Tests/Bar/BazTests.cs`
   - Add to existing file if tests for that class already exist.
3. Write tests that:
   - Cover the happy path, boundary values, and at least one failure/edge case
   - Use descriptive method names: `MethodName_Condition_ExpectedResult`
   - Use `[Fact]` for deterministic cases, `[Theory]` + `[InlineData]` for parameterised
   - Assert on observable output only — no implementation details
   - Do NOT create stub/fake implementations to make them compile; use `#pragma warning disable` or `// TODO: implement` comments if the type doesn't exist yet
4. Run `dotnet build src/` — the build **must fail** or tests **must fail** (red). If they pass, the tests are not testing anything new; revise them.
5. Report: which tests were added, which assertion checks what behavior, and what the build/test failure is.

## Guidelines

- One test class per production class
- Keep each test focused on a single behavior
- Prefer `Assert.Equal` / `Assert.True` over `Assert.NotNull` unless null-safety is the behavior under test
- Avoid mocking framework internals; test at the public API surface
- File-scoped namespaces, match the production namespace with `.Tests` appended
