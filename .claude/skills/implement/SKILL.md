---
name: implement
description: >
  Write the minimal production code to make failing tests pass (TDD Green phase).
  Triggers: "implement X", "make the tests pass", "write the code for", "make it compile",
  or after failing tests need production code.
---

Spawn a `developer` agent with the user's input as the target test class or file.
If no target specified, tell the agent to run all failing tests.
Report the agent's output to the user.
