---
name: port
description: >
  Port a Python Hypothesis module to idiomatic C# for the Conjecture.NET project.
  Use this skill whenever the user wants to translate, adapt, or port code from the Python Hypothesis library into C# — even if they don't say "port" explicitly.
  Triggers on phrases like "port X from hypothesis", "translate this Python module", "implement X based on hypothesis", "bring over the hypothesis shrinking logic", or when referencing a specific Python Hypothesis file or module to add to Conjecture.NET.
---

Port a Python Hypothesis module to idiomatic C#.

## Input

Path or module name from the Python Hypothesis repo (e.g., `hypothesis/strategies/numbers.py` or `conjecture/engine`).

## Steps

1. Fetch or read the Python source from https://github.com/HypothesisWorks/hypothesis. Use the `hypothesis-python/src/hypothesis/` subtree.
2. Analyze the module: identify public API, internal helpers, dependencies on other Hypothesis modules, and Python-specific idioms.
3. Design the C# equivalent:
   - Map Python decorators → attributes or fluent API
   - Map generators/yield → `IEnumerable<T>`, `Span<T>`, or custom iterators
   - Map Python's dynamic typing → generics with constraints
   - Map coroutine-based shrinking → iterative or callback-based approach
   - Use .NET naming conventions (PascalCase methods, `camelCase` fields)
   - Use file-scoped namespaces
   - Prefer `readonly struct` where appropriate
4. Flag any areas requiring a design decision (e.g., no direct .NET equivalent exists). Suggest options with trade-offs.
5. **Add the standard project header plus Hypothesis attribution** at the top of every C# file derived from Hypothesis source (see ADR-0032):
   ```csharp
   // Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
   // See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/
   //
   // This file is derived from the Python Hypothesis library.
   // Original copyright: Copyright (c) 2013-present, David R. MacIver and contributors.
   ```
6. Write the C# file(s) into the appropriate location under `src/`.
7. Write corresponding unit tests.
8. Summarize what was ported, what diverged from Python, and any open design questions.

## Guidelines

- Prioritize idiomatic .NET over 1:1 translation — the API should feel native to C# developers.
- All source files are MPL-2.0 (ADR-0031). Files derived from Hypothesis must additionally include the attribution comment (ADR-0032).
- Reference `Directory.Packages.props` for any new NuGet dependencies.
- Check `docs/decisions/` for prior ADRs that constrain the design.
- Keep the port minimal — don't add features the Python version doesn't have.
