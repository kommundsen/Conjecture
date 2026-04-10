---
name: test
description: >
  Write failing xUnit tests for a behavior (TDD Red phase).
  Triggers: "write tests for X", "add coverage for X", "test that X does Y",
  "I need a failing test", or naming a feature that needs tests.
  Do NOT trigger when the user wants to make existing failing tests pass (use implement),
  refactor existing tests (use simplify), or simply run/explain tests.
---

Spawn a `test-developer` agent with the user's behavior description.
Include the target test file path if the user specified one.
Report the agent's output to the user.
