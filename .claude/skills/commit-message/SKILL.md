---
name: commit-message
description: >
  Suggest a descriptive git commit message for the current changes. Use this skill
  whenever the user asks for a commit message, wants to know what to write for a commit,
  or says something like "what should my commit say", "suggest a message", or "write a
  commit message". DO NOT actually commit — just output the message. Invoke this skill
  proactively whenever the user is about to commit or asks about committing.
---

# Commit Message Suggester

Generate a clear, descriptive commit message for the staged (or unstaged) changes.

## Steps

1. **Get the diff**
   - Run `git diff --cached` to see staged changes. If there are staged changes, use those — stop here.
   - If nothing is staged, run `git status --short` to find untracked files, then `git diff --no-index /dev/null <file>` (or read them directly) to understand what they contain.
   - If there are neither staged changes nor untracked files, report that there is nothing to commit.

2. **Understand what changed**
   - Read the diff carefully. Identify: which files changed, what was added/removed/modified, and why (infer from context and naming).
   - Look at the *intent* of the change, not just the mechanics. A method being added to a class is less useful than knowing *what that method does and why it exists*.

3. **Choose the right verb**
   Pick the one that best fits the nature of the change:

   | Verb | When to use |
   |---|---|
   | `Adds` | New feature, method, class, file, or capability |
   | `Implements` | A specific interface, spec, or design pattern is being realised |
   | `Refactors` | Structure improved without changing behavior |
   | `Fixes` | Bug corrected |
   | `Improves` | Enhancement to something that already works |
   | `Removes` | Code deleted without replacement |
   | `Extracts` | Code moved out to its own file, class, or method |
   | `Documents` | Comments, README, ADR, or docs only |
   | `Updates` | Dependency, config, or version bump |

   If multiple things changed, pick the verb for the most significant change.

4. **Write the subject line**
   - Format: `Verb <what> [to/for/from <why or where>]`
   - Start with the chosen verb (capitalised, no period)
   - Name the *thing* that changed specifically — class name, method name, module name
   - If there's a meaningful *why* or *where*, include it briefly
   - Keep it under 72 characters
   - No ticket numbers, no "Cycle X.Y.Z" references

   Good examples:
   - `Adds Delete method to ExampleDatabase`
   - `Refactors ExampleDatabase to use parameterised Execute helper`
   - `Fixes off-by-one in IntegerStrategy shrink loop`
   - `Extracts TestIdHasher into its own file`
   - `Documents IEEE 754 floating-point strategy design in ADR-0027`

   Bad examples (too vague or too cycle-centric):
   - `Implements Cycle 1.9.3` ❌
   - `Updates code` ❌
   - `Fix bug` ❌

5. **Write the body (if the change warrants it)**
   - Include a body when the change is non-trivial and the *why* isn't obvious from the subject.
   - Use short bullet points or a brief paragraph.
   - Describe key changes: which behaviour was added, what the design choice was, any caveats.
   - Separate from subject with a blank line.

6. **Output the message**
   Present the message in a code block so it's easy to copy. If there's a meaningful body, include it. If the change is simple and self-explanatory, subject-only is fine.

   ```
   Adds Delete method to ExampleDatabase

   - Removes all stored buffers for a given test ID hash
   - No-op for unknown keys (DELETE WHERE is safe with no matching rows)
   ```

## Guidelines

- Describe the *effect*, not the *mechanism*. "Adds replay of stored failures before random search" is better than "Adds call to db.Load() in TestRunner.Run()".
- One commit message per invocation — if the diff contains many unrelated changes, note that and suggest the user stage them separately.
- Never add `Co-authored-by` or any trailers — that's the user's job.
- Never include the command to commit — just the message text.
