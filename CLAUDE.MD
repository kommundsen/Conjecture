- Be extremely concise in your responses. Sacrifice grammer for concision
- Never auto-commit code. Commits are to be done by user.
- Follow .editorconfig rules when writing code.
- Use workspace-relative paths in all tool calls (not absolute paths)

## Build & Test

`dotnet format` runs automatically via hook after every .cs file edit — no need to run manually.

```bash
dotnet build src/                                                     # check for warnings
dotnet test src/ --filter "FullyQualifiedName~Target" --no-build      # safe after build
dotnet test src/                                                      # full suite (review only)
```

## Code Style

Rules enforced as build errors — write them correctly the first time:

| Pattern | Correct | Wrong | IDE |
|---|---|---|---|
| File header | `// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.` + newline + `// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/` | Missing header | IDE0073 |
| Namespace | `namespace Foo.Bar;` | `namespace Foo.Bar { }` | IDE0160 |
| `var` | never — always explicit types | `var x = new Foo()` | — |
| Object creation | `new()` when type is apparent | `new Foo()` on right side | IDE0090 |
| Braces | always, even single-line `if`/`for` | `if (x) return;` | IDE0011 |
| `using` | `using var x = …;` | `using (var x = …) { }` | IDE0063 |
| `null` check | `x is null` / `x is not null` | `x == null` / `x != null` | IDE0041 |
| `not` pattern | `x is not null` | `!(x is null)` | IDE0083 |
| Null propagation | `x?.Foo` | `x == null ? null : x.Foo` | IDE0031 |
| Null coalescing | `x ?? y` | `x is null ? y : x` | IDE0029 |
| Ternary assign | `x = cond ? a : b;` | `if (cond) x = a; else x = b;` | IDE0045 |
| Ternary return | `return cond ? a : b;` | `if (cond) return a; return b;` | IDE0046 |
| Switch | `x switch { … }` | `switch (x) { … }` when all arms return | IDE0066 |
| Default | `default` | `default(T)` | IDE0034 |
| Index | `arr[^1]` | `arr[arr.Length - 1]` | IDE0056 |
| Range | `arr[1..^1]` | `arr.Skip(1).Take(…)` | IDE0057 |
| Local function | `static void F() { }` | `Action f = () => …` | IDE0039 |
| Static lambda | `static () => …` | `() => …` (when no captures) | IDE0258 |
| Primary ctor | `class Foo(int x)` | ctor + field assignment | IDE0290 |
| Throw expression | `x ?? throw new …` | `if (x is null) throw …` | IDE0016 |
| Expression prop | `int X => _x;` | `int X { get { return _x; } }` | — |
| Expression method | block body `{ return …; }` | `=> expr` (methods must use block bodies) | — |
| Pattern matching | `x is Foo f` | `x is Foo; var f = (Foo)x` | IDE0019/20 |
| Readonly struct | `readonly struct` / `readonly` members | Mutable struct when immutable | IDE0250/251 |

## Project Structure

Solution file: `src/Conjecture.slnx`

### Production → Test project mapping

| Production project | Test project |
|--------------------|-------------|
| `src/Conjecture.Core/` | `src/Conjecture.Core.Tests/` |
| `src/Conjecture.Xunit/` | `src/Conjecture.Xunit.Tests/` |
| `src/Conjecture.Xunit.V3/` | `src/Conjecture.Xunit.V3.Tests/` |
| `src/Conjecture.NUnit/` | `src/Conjecture.NUnit.Tests/` |
| `src/Conjecture.MSTest/` | `src/Conjecture.MSTest.Tests/` |
| `src/Conjecture.Generators/` | `src/Conjecture.Generators.Tests/` |
| `src/Conjecture.Analyzers/` | `src/Conjecture.Analyzers.Tests/` |
| `src/Conjecture.Mcp/` | `src/Conjecture.Mcp.Tests/` |
| `src/Conjecture.Tool/` | `src/Conjecture.Tool.Tests/` |
| `src/Conjecture.FSharp/` | `src/Conjecture.FSharp.Tests/` |
| `src/Conjecture.FSharp.Expecto/` | `src/Conjecture.FSharp.Expecto.Tests/` |

New test files go in the paired test project, mirroring the production file's relative path.

### Other locations

- Architecture decisions (ADRs): `docs/decisions/`
- Documentation site: `docs/site`
- Public API surface: each non-test project has `PublicAPI.Unshipped.txt` — new public symbols must be declared here (enforced by RS0016 at build time; the compiler error includes the exact signature to copy in)
- GitHub repo: `kommundsen/Conjecture`
