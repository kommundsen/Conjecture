Port a Python Hypothesis module to idiomatic C#.

## Input

$ARGUMENTS — path or module name from the Python Hypothesis repo (e.g., `hypothesis/strategies/numbers.py` or `conjecture/engine`)

## Steps

1. Fetch or read the Python source from https://github.com/HypothesisWorks/hypothesis. Use the `hypothesis-python/src/hypothesis/` subtree.
2. Analyze the module: identify public API, internal helpers, dependencies on other Hypothesis modules, and Python-specific idioms.
3. Design the C# equivalent:
   - Map Python decorators → attributes or fluent API
   - Map generators/yield → `IEnumerable<T>`, `Span<T>`, or custom iterators
   - Map Python's dynamic typing → generics with constraints
   - Map coroutine-based shrinking → iterative or callback-based approach
   - Use .NET naming conventions (PascalCase methods, `_camelCase` fields)
   - Use file-scoped namespaces
   - Prefer `readonly struct` where appropriate
4. Flag any areas requiring a design decision (e.g., no direct .NET equivalent exists). Suggest options with trade-offs.
5. Write the C# file(s) into the appropriate location under `src/`.
6. Write corresponding unit tests.
7. Summarize what was ported, what diverged from Python, and any open design questions.

## Guidelines

- Prioritize idiomatic .NET over 1:1 translation. The API should feel native to C# developers.
- Reference `Directory.Packages.props` for any new NuGet dependencies.
- Check `docs/decisions/` for prior ADRs that constrain the design.
- Keep the port minimal — don't add features the Python version doesn't have.
