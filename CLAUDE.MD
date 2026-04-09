- Be extremely concise in your responses. Sacrifice grammer for concision
- Never auto-commit code. Commits are to be done by user.
- Follow .editorconfig rules when writing code.
- Use workspace-relative paths in all tool calls (not absolute paths)

## Build & Test

```bash
dotnet build src/
dotnet test src/
dotnet test src/ --filter "FullyQualifiedName~SomeTest"
```
## Format

```bash
dotnet format src/ --include .somedir/somefile.cs .somedir/somefile2.cs
```

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